using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;
using MobileBill.Domain.Calculations;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Reports;

// Value Added Services report: every number with a VAS charge in a matched batch, with who holds it, how much of the
// bill the VAS is, and whether the number had VAS in the earlier months too (repeat users). Read-only: nothing is saved.
public sealed class VasReportService(MobileBillDbContext db, IClock clock) : IVasReportService
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string NotMatched = "Not matched";

    static VasReportService() => ReportFonts.Configure();

    public async Task<IReadOnlyList<VasBatchDto>> GetBatchesAsync(CancellationToken cancellationToken)
    {
        var batches = await MatchedBatches().AsNoTracking()
            .OrderByDescending(batch => batch.BillingYear).ThenByDescending(batch => batch.BillingMonth)
            .Select(batch => new { batch.Id, batch.BillingYear, batch.BillingMonth, Provider = batch.Provider.Name, batch.Status })
            .ToListAsync(cancellationToken);
        return batches.Select(batch => new VasBatchDto(batch.Id, batch.BillingYear, batch.BillingMonth, batch.Provider, batch.Status, IsPreliminary(batch.Status))).ToList();
    }

    public async Task<VasReportDto> GetAsync(VasReportRequest request, CancellationToken cancellationToken)
    {
        var batch = await db.BillBatches.AsNoTracking().Include(item => item.Provider).SingleOrDefaultAsync(item => item.Id == request.BatchId, cancellationToken)
            ?? throw new BillingReportNotFoundException(request.BatchId);
        if (!await MatchedBatches().AnyAsync(item => item.Id == batch.Id, cancellationToken))
            throw new BillingReportConflictException("The VAS report is available once the batch has been matched to employees.");
        await RequireKnownCodesAsync(request, cancellationToken);

        // The two matched batches before this one give the repeat-use history and the month-on-month change.
        var period = batch.BillingYear * 12 + batch.BillingMonth;
        var earlier = await MatchedBatches().AsNoTracking()
            .Where(item => item.BillingYear * 12 + item.BillingMonth < period)
            .OrderByDescending(item => item.BillingYear).ThenByDescending(item => item.BillingMonth)
            .Select(item => item.Id).Take(VasRules.RepeatWindowMonths - 1).ToListAsync(cancellationToken);
        var earlierVas = await db.BillLines.AsNoTracking()
            .Where(line => earlier.Contains(line.BillBatchId) && line.ExtractionStatus == BillLineExtractionStatus.Extracted && line.ValueAddedServices > 0m)
            .Select(line => new { line.BillBatchId, line.MobileNumber }).Distinct().ToListAsync(cancellationToken);
        var monthsBefore = earlierVas.GroupBy(item => item.MobileNumber).ToDictionary(group => group.Key, group => group.Count());

        var current = Filter(await LoadLinesAsync(batch.Id, cancellationToken), request).ToList();
        var rows = current.Select(line =>
        {
            var months = 1 + monthsBefore.GetValueOrDefault(line.MobileNumber);
            return new VasReportRowDto(line.MobileNumber, line.Epf, line.EmployeeName, line.CallingName, line.Factory, line.Department, line.Section, line.Category,
                line.Vas, line.ActualBill, VasRules.ShareOfBill(line.Vas, line.ActualBill), line.Responsibility, line.FinalDeduction,
                months, 1 + earlier.Count, VasRules.IsRepeatUser(months), line.IsPooled, line.IsMatched);
        })
            .Where(row => request.MinimumVas is null || row.Vas >= request.MinimumVas)
            .Where(row => !request.RepeatOnly || row.IsRepeat)
            .OrderByDescending(row => row.Vas).ThenBy(row => row.MobileNumber, StringComparer.Ordinal)
            .ToList();

        int? previousUsers = null; decimal? previousTotal = null;
        if (earlier.Count > 0)
        {
            var previous = Filter(await LoadLinesAsync(earlier[0], cancellationToken), request)
                .Where(line => request.MinimumVas is null || line.Vas >= request.MinimumVas).ToList();
            previousUsers = previous.Count;
            previousTotal = previous.Sum(line => line.Vas);
        }

        return new VasReportDto(batch.Id, batch.BillingYear, batch.BillingMonth, batch.Provider.Name, batch.Status, IsPreliminary(batch.Status),
            rows.Count, rows.Sum(row => row.Vas), rows.Count(row => row.IsRepeat), previousUsers, previousTotal,
            Group(rows, row => row.Factory), Group(rows, row => row.Department), rows);
    }

    public async Task<ReportFile> ExportExcelAsync(VasReportRequest request, CancellationToken cancellationToken)
    {
        var report = await GetAsync(request, cancellationToken);
        using var book = new XLWorkbook();
        AddVasSheet(book, report, "VAS Report", clock.UtcNow, FilterLabel(request));
        AddSummarySheet(book, report);
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return new ReportFile(FileName(report, request, "xlsx"), ExcelContentType, stream.ToArray());
    }

    public async Task<ReportFile> ExportPdfAsync(VasReportRequest request, CancellationToken cancellationToken)
    {
        var report = await GetAsync(request, cancellationToken);
        var renderer = new PdfDocumentRenderer { Document = BuildPdf(report, FilterLabel(request)) };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return new ReportFile(FileName(report, request, "pdf"), PdfBillingReportService.PdfContentType, stream.ToArray());
    }

    private static readonly string[] Headers =
    [
        "Serial", "Mobile Number", "EPF", "Name", "Calling Name", "Factory", "Department", "Section", "Category",
        "VAS", "Actual Bill", "VAS % of Bill", "Responsibility", "Final Deduction", "Months with VAS", "Repeat User", "Note"
    ];

    // Writes the VAS rows onto a worksheet; also used for the optional VAS sheet of the Monthly Bill Report.
    internal static void AddVasSheet(XLWorkbook book, VasReportDto report, string sheetName, DateTimeOffset generatedAt, string filterLabel)
    {
        const int headerRow = 6;
        var sheet = book.Worksheets.Add(sheetName);
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 10;
        sheet.Cell(1, 1).Value = "Value Added Services (VAS) Report";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = "Billing Period"; sheet.Cell(2, 2).Value = Period(report);
        sheet.Cell(2, 4).Value = "Provider"; sheet.Cell(2, 5).Value = report.Provider;
        sheet.Cell(3, 1).Value = "Batch Status"; sheet.Cell(3, 2).Value = report.IsPreliminary ? $"{report.BatchStatus} (Preliminary: not yet approved)" : report.BatchStatus.ToString();
        sheet.Cell(3, 4).Value = "Filters"; sheet.Cell(3, 5).Value = filterLabel;
        sheet.Cell(4, 1).Value = "Generated (UTC)"; sheet.Cell(4, 2).Value = generatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        sheet.Cell(4, 4).Value = "Repeat User"; sheet.Cell(4, 5).Value = $"VAS in at least {VasRules.RepeatMinimumMonths} of the last {VasRules.RepeatWindowMonths} billing months";
        foreach (var cell in new[] { sheet.Cell(2, 1), sheet.Cell(2, 4), sheet.Cell(3, 1), sheet.Cell(3, 4), sheet.Cell(4, 1), sheet.Cell(4, 4) }) cell.Style.Font.Bold = true;
        sheet.Cell(5, 1).Value = BillingReportData.SystemGeneratedNotice;
        sheet.Cell(5, 1).Style.Font.Italic = true;

        for (var column = 1; column <= Headers.Length; column++) sheet.Cell(headerRow, column).Value = Headers[column - 1];
        var header = sheet.Range(headerRow, 1, headerRow, Headers.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;
        header.Style.Alignment.WrapText = true;

        for (var index = 0; index < report.Rows.Count; index++)
        {
            var row = report.Rows[index];
            var target = headerRow + 1 + index;
            sheet.Cell(target, 1).Value = index + 1;
            sheet.Cell(target, 2).Value = row.MobileNumber;
            sheet.Cell(target, 3).Value = row.Epf ?? string.Empty;
            sheet.Cell(target, 4).Value = row.EmployeeName ?? string.Empty;
            sheet.Cell(target, 5).Value = row.CallingName ?? string.Empty;
            sheet.Cell(target, 6).Value = row.Factory ?? string.Empty;
            sheet.Cell(target, 7).Value = row.Department ?? string.Empty;
            sheet.Cell(target, 8).Value = row.Section ?? string.Empty;
            sheet.Cell(target, 9).Value = row.Category ?? string.Empty;
            sheet.Cell(target, 10).Value = row.Vas;
            sheet.Cell(target, 11).Value = row.ActualBill;
            sheet.Cell(target, 12).Value = row.VasShareOfBill / 100m;
            sheet.Cell(target, 13).Value = ResponsibilityLabel(row.Responsibility);
            if (row.FinalDeduction is { } deduction) sheet.Cell(target, 14).Value = deduction;
            sheet.Cell(target, 15).Value = $"{row.MonthsWithVas} of {row.MonthsConsidered}";
            sheet.Cell(target, 16).Value = row.IsRepeat ? "Yes" : "No";
            sheet.Cell(target, 17).Value = Note(row);
        }
        var lastRow = headerRow + report.Rows.Count;
        var totals = lastRow + 1;
        sheet.Cell(totals, 1).Value = "Totals";
        sheet.Cell(totals, 2).Value = $"{report.Users} numbers";
        sheet.Cell(totals, 10).Value = report.TotalVas;
        sheet.Cell(totals, 11).Value = report.Rows.Sum(row => row.ActualBill);
        sheet.Range(totals, 1, totals, Headers.Length).Style.Font.Bold = true;
        sheet.Range(totals, 1, totals, Headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Double;
        sheet.Range(headerRow + 1, 10, totals, 11).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 14, totals, 14).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 12, totals, 12).Style.NumberFormat.Format = "0.0%";
        sheet.Range(headerRow + 1, 2, Math.Max(lastRow, headerRow + 1), 3).Style.NumberFormat.Format = "@";
        sheet.Cell(totals + 2, 1).Value = BillingReportData.SystemGeneratedNotice;
        sheet.Cell(totals + 2, 1).Style.Font.Italic = true;
        var widths = new[] { 8d, 15d, 12d, 28d, 16d, 18d, 20d, 18d, 16d, 13d, 13d, 11d, 16d, 14d, 12d, 11d, 30d };
        for (var column = 1; column <= widths.Length; column++) sheet.Column(column).Width = widths[column - 1];
        sheet.SheetView.FreezeRows(headerRow);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Footer.Center.AddText(BillingReportData.SystemGeneratedNotice);
    }

    private static void AddSummarySheet(XLWorkbook book, VasReportDto report)
    {
        var sheet = book.Worksheets.Add("Summary");
        sheet.Style.Font.FontName = "Arial";
        sheet.Cell(1, 1).Value = $"VAS Summary – {Period(report)}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        var rows = new List<(string Label, object Value)>
        {
            ("Numbers with VAS", report.Users), ("Total VAS", report.TotalVas), ("Repeat users", report.RepeatUsers),
        };
        if (report.PreviousTotalVas is { } previousTotal)
        {
            rows.Add(("Previous month: numbers with VAS", report.PreviousUsers ?? 0));
            rows.Add(("Previous month: total VAS", previousTotal));
            rows.Add(("Change in total VAS", report.TotalVas - previousTotal));
        }
        var line = 3;
        foreach (var (label, value) in rows)
        {
            sheet.Cell(line, 1).Value = label;
            sheet.Cell(line, 1).Style.Font.Bold = true;
            if (value is decimal amount) { sheet.Cell(line, 2).Value = amount; sheet.Cell(line, 2).Style.NumberFormat.Format = "#,##0.00"; }
            else sheet.Cell(line, 2).Value = (int)value;
            line++;
        }
        line = WriteGroups(sheet, line + 1, "By Factory", report.ByFactory);
        WriteGroups(sheet, line + 1, "By Department", report.ByDepartment);
        sheet.Column(1).Width = 36;
        sheet.Column(2).Width = 14;
        sheet.Column(3).Width = 16;
    }

    private static int WriteGroups(IXLWorksheet sheet, int startRow, string title, IReadOnlyList<VasGroupDto> groups)
    {
        sheet.Cell(startRow, 1).Value = title;
        sheet.Cell(startRow, 1).Style.Font.Bold = true;
        sheet.Cell(startRow + 1, 1).Value = "Name"; sheet.Cell(startRow + 1, 2).Value = "Numbers"; sheet.Cell(startRow + 1, 3).Value = "Total VAS";
        sheet.Range(startRow + 1, 1, startRow + 1, 3).Style.Font.Bold = true;
        var row = startRow + 2;
        foreach (var group in groups)
        {
            sheet.Cell(row, 1).Value = group.Name; sheet.Cell(row, 2).Value = group.Users; sheet.Cell(row, 3).Value = group.TotalVas;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }
        return row;
    }

    private static Document BuildPdf(VasReportDto report, string filterLabel)
    {
        var document = new Document();
        document.Info.Title = $"VAS Report - {Period(report)}";
        document.Info.Subject = BillingReportData.SystemGeneratedNotice;
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 6.5;
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromMillimeter(10);
        section.PageSetup.TopMargin = Unit.FromMillimeter(10);
        section.PageSetup.BottomMargin = Unit.FromMillimeter(16);
        section.PageSetup.FooterDistance = Unit.FromMillimeter(6);

        var title = section.AddParagraph("Value Added Services (VAS) Report");
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        var info = section.AddParagraph();
        info.AddFormattedText("Billing Period: ", TextFormat.Bold); info.AddText(Period(report));
        info.AddFormattedText("    Provider: ", TextFormat.Bold); info.AddText(report.Provider);
        info.AddFormattedText("    Status: ", TextFormat.Bold); info.AddText(report.IsPreliminary ? $"{report.BatchStatus} (Preliminary)" : report.BatchStatus.ToString());
        info.AddFormattedText("    Filters: ", TextFormat.Bold); info.AddText(filterLabel);
        var summary = section.AddParagraph();
        summary.Format.SpaceAfter = Unit.FromPoint(6);
        summary.AddFormattedText($"Numbers with VAS: {report.Users}    Total VAS: {Money(report.TotalVas)}    Repeat users: {report.RepeatUsers}", TextFormat.Bold);
        if (report.PreviousTotalVas is { } previous) summary.AddText($"    (previous month: {report.PreviousUsers} numbers, {Money(previous)})");

        double[] widths = [8, 20, 14, 42, 26, 26, 24, 20, 20, 13, 20, 14, 30];
        string[] headings = ["#", "Mobile", "EPF", "Name", "Factory", "Department", "Section", "VAS", "Actual Bill", "VAS %", "Responsibility", "Months", "Note"];
        var table = section.AddTable();
        table.Borders.Bottom.Width = 0.25;
        table.Borders.Bottom.Color = Color.Parse("#D9E2F3");
        table.TopPadding = table.BottomPadding = Unit.FromPoint(1.5);
        foreach (var width in widths) table.AddColumn(Unit.FromMillimeter(width));
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Color.Parse("#1F4E78");
        header.Format.Font.Color = Colors.White;
        header.Format.Font.Bold = true;
        for (var column = 0; column < headings.Length; column++) header.Cells[column].AddParagraph(headings[column]);
        for (var index = 0; index < report.Rows.Count; index++)
        {
            var item = report.Rows[index];
            var row = table.AddRow();
            string?[] values = [(index + 1).ToString(CultureInfo.InvariantCulture), item.MobileNumber, item.Epf, item.EmployeeName, item.Factory, item.Department, item.Section,
                Money(item.Vas), Money(item.ActualBill), $"{item.VasShareOfBill:0.0}%", ResponsibilityLabel(item.Responsibility), $"{item.MonthsWithVas}/{item.MonthsConsidered}{(item.IsRepeat ? " repeat" : string.Empty)}", Note(item)];
            for (var column = 0; column < values.Length; column++)
                if (!string.IsNullOrEmpty(values[column]))
                {
                    var paragraph = row.Cells[column].AddParagraph(values[column]!);
                    if (column is 7 or 8 or 9) paragraph.Format.Alignment = ParagraphAlignment.Right;
                }
        }
        var totals = table.AddRow();
        totals.Format.Font.Bold = true;
        totals.Cells[0].AddParagraph("Totals");
        totals.Cells[7].AddParagraph(Money(report.TotalVas)).Format.Alignment = ParagraphAlignment.Right;
        totals.Cells[8].AddParagraph(Money(report.Rows.Sum(row => row.ActualBill))).Format.Alignment = ParagraphAlignment.Right;

        var closing = section.AddParagraph(BillingReportData.SystemGeneratedNotice);
        closing.Format.Font.Italic = true;
        closing.Format.SpaceBefore = Unit.FromPoint(10);
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText(BillingReportData.SystemGeneratedNotice + "   |   Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();
        return document;
    }

    // A batch counts as matched once matching has created its monthly bills or exceptions.
    private IQueryable<Domain.Entities.BillBatch> MatchedBatches() => db.BillBatches
        .Where(batch => db.MonthlyBills.Any(bill => bill.BillLine.BillBatchId == batch.Id) || db.BillExceptions.Any(exception => exception.BillBatchId == batch.Id));

    private async Task<List<VasLine>> LoadLinesAsync(Guid batchId, CancellationToken token) =>
        (await db.BillLines.AsNoTracking()
            .Where(line => line.BillBatchId == batchId && line.ExtractionStatus == BillLineExtractionStatus.Extracted && line.ValueAddedServices > 0m)
            .Select(line => new
            {
                line.MobileNumber, line.ValueAddedServices, line.TotalDueAmount, Bill = line.MonthlyBill == null ? null : new
                {
                    line.MonthlyBill.EmployeeEpfSnapshot, line.MonthlyBill.EmployeeNameSnapshot, line.MonthlyBill.CallingNameSnapshot,
                    line.MonthlyBill.FactoryCodeSnapshot, line.MonthlyBill.FactoryNameSnapshot, line.MonthlyBill.DepartmentNameSnapshot,
                    line.MonthlyBill.SectionCodeSnapshot, line.MonthlyBill.SectionNameSnapshot, line.MonthlyBill.CategoryCodeSnapshot, line.MonthlyBill.CategoryNameSnapshot,
                    line.MonthlyBill.Responsibility, line.MonthlyBill.FinalDeduction, line.MonthlyBill.IsPooled
                }
            })
            .ToListAsync(token))
        .Select(line => line.Bill is null
            ? new VasLine(line.MobileNumber, line.ValueAddedServices, line.TotalDueAmount, false, null, null, null, null, null, null, null, null, null, null, null, null, false)
            : new VasLine(line.MobileNumber, line.ValueAddedServices, line.TotalDueAmount, true, line.Bill.EmployeeEpfSnapshot, line.Bill.EmployeeNameSnapshot, line.Bill.CallingNameSnapshot,
                line.Bill.FactoryCodeSnapshot, line.Bill.FactoryNameSnapshot, line.Bill.DepartmentNameSnapshot, line.Bill.SectionCodeSnapshot, line.Bill.SectionNameSnapshot,
                line.Bill.CategoryCodeSnapshot, line.Bill.CategoryNameSnapshot, line.Bill.Responsibility, line.Bill.Responsibility is null ? null : line.Bill.FinalDeduction, line.Bill.IsPooled))
        .ToList();

    // Factory, category and section filters need an employee, so unmatched lines appear only in the unfiltered report.
    private static IEnumerable<VasLine> Filter(IEnumerable<VasLine> lines, VasReportRequest request)
    {
        var factories = Codes(request.FactoryCodes); var categories = Codes(request.CategoryCodes); var sections = Codes(request.SectionCodes);
        return lines.Where(line =>
            (factories.Count == 0 || (line.FactoryCode is not null && factories.Contains(line.FactoryCode)))
            && (categories.Count == 0 || (line.CategoryCode is not null && categories.Contains(line.CategoryCode)))
            && (sections.Count == 0 || (line.SectionCode is not null && sections.Contains(line.SectionCode))));
    }

    private async Task RequireKnownCodesAsync(VasReportRequest request, CancellationToken token)
    {
        foreach (var code in Codes(request.FactoryCodes)) if (!await db.Factories.AnyAsync(item => item.Code == code, token)) throw new BillingReportFilterNotFoundException("Factory", code);
        foreach (var code in Codes(request.CategoryCodes)) if (!await db.EmployeeCategories.AnyAsync(item => item.Code == code, token)) throw new BillingReportFilterNotFoundException("Category", code);
        foreach (var code in Codes(request.SectionCodes)) if (!await db.Sections.AnyAsync(item => item.Code == code, token)) throw new BillingReportFilterNotFoundException("Section", code);
    }

    private static HashSet<string> Codes(IReadOnlyList<string>? codes) =>
        (codes ?? []).Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<VasGroupDto> Group(IEnumerable<VasReportRowDto> rows, Func<VasReportRowDto, string?> key) => rows
        .GroupBy(row => row.IsMatched ? key(row) ?? "—" : NotMatched)
        .Select(group => new VasGroupDto(group.Key, group.Count(), group.Sum(row => row.Vas)))
        .OrderByDescending(group => group.TotalVas).ThenBy(group => group.Name, StringComparer.Ordinal).ToList();

    private static bool IsPreliminary(BillBatchStatus status) => status is not (BillBatchStatus.Completed or BillBatchStatus.Locked);

    private static string FilterLabel(VasReportRequest request)
    {
        var parts = new List<string>();
        if (Codes(request.FactoryCodes) is { Count: > 0 } factories) parts.Add($"Factory {string.Join(", ", factories)}");
        if (Codes(request.CategoryCodes) is { Count: > 0 } categories) parts.Add($"Category {string.Join(", ", categories)}");
        if (Codes(request.SectionCodes) is { Count: > 0 } sections) parts.Add($"Section {string.Join(", ", sections)}");
        if (request.MinimumVas is { } minimum) parts.Add($"VAS at least {Money(minimum)}");
        if (request.RepeatOnly) parts.Add("Repeat users only");
        return parts.Count == 0 ? "None (all numbers with VAS)" : string.Join("; ", parts);
    }

    private static string FileName(VasReportDto report, VasReportRequest request, string extension)
    {
        var suffix = string.Concat(Codes(request.FactoryCodes).Concat(Codes(request.CategoryCodes)).Concat(Codes(request.SectionCodes)).Select(code => $"_{code}"));
        return $"VAS_Report_{report.BillingYear:D4}_{report.BillingMonth:D2}{suffix}{(request.RepeatOnly ? "_Repeat" : string.Empty)}.{extension}";
    }

    internal static string Note(VasReportRowDto row) => !row.IsMatched ? NotMatched : row.IsPooled ? "SIM Pool (no holder)" : string.Empty;
    private static string ResponsibilityLabel(Responsibility? responsibility) => responsibility switch { Responsibility.ByUser => "By User", Responsibility.ByCompany => "By Company", _ => "Unassessed" };
    private static string Period(VasReportDto report) => new DateTime(report.BillingYear, report.BillingMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    private static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private sealed record VasLine(
        string MobileNumber, decimal Vas, decimal ActualBill, bool IsMatched, string? Epf, string? EmployeeName, string? CallingName,
        string? FactoryCode, string? Factory, string? Department, string? SectionCode, string? Section, string? CategoryCode, string? Category,
        Responsibility? Responsibility, decimal? FinalDeduction, bool IsPooled);
}
