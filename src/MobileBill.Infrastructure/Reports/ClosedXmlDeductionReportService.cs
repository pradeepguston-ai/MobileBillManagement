using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Billing;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Reports;

// Payroll deduction sheet: one row per employee with the By User deduction of all their numbers added together.
// Salary Advance, Festival Advance and Annual BOI Entry Permit are left empty for payroll to fill in.
public sealed class ClosedXmlDeductionReportService(MobileBillDbContext db, IClock clock) : IBillingDeductionReportService
{
    private const string MoneyFormat = "#,##0.00";
    // Totals of the empty columns show "-" rather than 0.00, as in the payroll sheet.
    private const string TotalFormat = "#,##0.00;-#,##0.00;\"-\"";
    private const int HeaderRow = 5;
    private static readonly (string Header, double Width)[] Columns =
    [
        ("SE NO", 7), ("EPF NO.", 11), ("EMPLOYEE NAME", 30), ("DESIGNATION", 40), ("PAYROLL CATEGORY", 20),
        ("SALARY ADVANCE (LKR)", 13), ("FESTIVAL ADVANCE (LKR)", 13), ("ANNUAL BOI ENTRY PERMIT (LKR)", 14), ("MOBILE BILL DEDUCTION (LKR)", 16), ("STATUS", 11),
    ];
    private const int NameColumn = 3, FirstAmountColumn = 6, DeductionColumn = 9, StatusColumn = 10;

    public async Task<DeductionReportFile> ExportAsync(Guid batchId, BillReviewRowsRequest filters, CancellationToken cancellationToken)
    {
        var batch = await LoadBatchAsync(batchId, cancellationToken);
        var bills = EfBillBatchReviewQueryService.Filter(db, BatchBills(batchId), filters);
        return await ExportAsync(batch, bills, await DescribeFiltersAsync(filters, cancellationToken), await FileNameSuffixAsync(filters, cancellationToken), cancellationToken);
    }

    // A Report Distribution rule's sheet: several factories and categories, empty meaning all (as in the rule's other reports).
    public async Task<DeductionReportFile> ExportForRuleAsync(Guid batchId, IReadOnlyCollection<string>? factoryCodes, IReadOnlyCollection<string>? categoryCodes, CancellationToken cancellationToken)
    {
        var batch = await LoadBatchAsync(batchId, cancellationToken);
        var factories = (factoryCodes ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
        var categories = (categoryCodes ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
        var bills = BatchBills(batchId);
        if (factories.Count > 0) bills = bills.Where(x => x.FactoryCodeSnapshot != null && factories.Contains(x.FactoryCodeSnapshot));
        if (categories.Count > 0) bills = bills.Where(x => x.CategoryCodeSnapshot != null && categories.Contains(x.CategoryCodeSnapshot));

        var factoryNames = await NamesAsync(factories, FactoryNameAsync, cancellationToken);
        var categoryNames = await NamesAsync(categories, CategoryNameAsync, cancellationToken);
        var parts = new List<string>();
        if (factories.Count > 0) parts.Add($"Factory {string.Join(", ", factoryNames)}");
        if (categories.Count > 0) parts.Add($"Category {string.Join(", ", categoryNames)}");
        var filterText = parts.Count == 0 ? "None (all employees with a By User deduction)" : string.Join("; ", parts);
        // Named like the rule's Monthly Bill Report attachment: factories by code, categories by name.
        var suffix = (factories.Count > 0 ? "_" + string.Join("-", factories.Select(FileNamePart)) : "") + (categories.Count > 0 ? "_" + string.Join("-", categoryNames.Select(FileNamePart)) : "");
        return await ExportAsync(batch, bills, filterText, suffix, cancellationToken);
    }

    private async Task<Domain.Entities.BillBatch> LoadBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.BillBatches.AsNoTracking().Include(x => x.Provider).SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new BillingReportNotFoundException(batchId);
        if (batch.Status is not (BillBatchStatus.Completed or BillBatchStatus.Locked))
            throw new BillingReportConflictException("The deduction report is available only for Completed or Locked bill batches.");
        return batch;
    }

    private IQueryable<Domain.Entities.MonthlyBill> BatchBills(Guid batchId) => db.MonthlyBills.AsNoTracking().Where(x => x.BillLine.BillBatchId == batchId);

    private async Task<DeductionReportFile> ExportAsync(Domain.Entities.BillBatch batch, IQueryable<Domain.Entities.MonthlyBill> filtered, string filterText, string fileNameSuffix, CancellationToken cancellationToken)
    {
        var bills = filtered.Where(x => x.Responsibility == Responsibility.ByUser && x.FinalDeduction > 0 && x.Status != MonthlyBillStatus.Excluded);
        var lines = await bills.Select(x => new
        {
            x.EmployeeId, x.MobileNumberSnapshot, Epf = x.EmployeeEpfSnapshot, x.EmployeeNameSnapshot,
            NameWithInitials = db.Employees.Where(e => e.Id == x.EmployeeId).Select(e => e.NameWithInitials).FirstOrDefault(),
            Designation = x.DesignationNameSnapshot, Category = x.CategoryNameSnapshot, x.FinalDeduction,
        }).ToListAsync(cancellationToken);

        // EPF numbers are unique only within a factory, so employees are grouped by their record.
        var rows = lines.GroupBy(x => x.EmployeeId).Select(group =>
        {
            var first = group.OrderBy(x => x.MobileNumberSnapshot, StringComparer.Ordinal).First();
            return new DeductionRow(first.Epf, string.IsNullOrWhiteSpace(first.NameWithInitials) ? first.EmployeeNameSnapshot : first.NameWithInitials!, first.Designation ?? "", first.Category ?? "", group.Sum(x => x.FinalDeduction));
        }).OrderBy(x => long.TryParse(x.Epf, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : long.MaxValue).ThenBy(x => x.Epf, StringComparer.Ordinal).ToList();

        var period = new DateTime(batch.BillingYear, batch.BillingMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var content = Build(rows, period, $"{batch.Provider.Name} · Corporate Code {batch.CorporateCode} · Generated {clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} (UTC)", filterText);
        var fileName = $"Mobile_Bill_Deduction_{batch.BillingYear:D4}_{batch.BillingMonth:D2}{fileNameSuffix}.xlsx";
        return new DeductionReportFile(fileName, ClosedXmlBillingExcelReportService.ExcelContentType, content, rows.Count, rows.Sum(x => x.Deduction));
    }

    private static byte[] Build(IReadOnlyList<DeductionRow> rows, string period, string subtitle, string filterText)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Mobile Bill Deduction");
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Calibri";
        sheet.Style.Font.FontSize = 11;
        var lastColumn = Columns.Length;

        sheet.Cell(1, 1).Value = $"Mobile Bill Deduction — {period}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = subtitle;
        sheet.Cell(3, 1).Value = $"Filters: {filterText}";
        sheet.Range(2, 1, 3, 1).Style.Font.FontColor = XLColor.FromHtml("#4A4F63");

        for (var column = 1; column <= lastColumn; column++)
        {
            sheet.Cell(HeaderRow, column).Value = Columns[column - 1].Header;
            sheet.Column(column).Width = Columns[column - 1].Width;
        }
        var header = sheet.Range(HeaderRow, 1, HeaderRow, lastColumn);
        header.Style.Font.Bold = true;
        header.Style.Alignment.WrapText = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(HeaderRow).Height = 54;

        var row = HeaderRow + 1;
        for (var index = 0; index < rows.Count; index++, row++)
        {
            var item = rows[index];
            sheet.Cell(row, 1).Value = index + 1;
            sheet.Cell(row, 1).Style.NumberFormat.Format = "00";
            sheet.Cell(row, 2).SetValue(item.Epf);   // text, so EPF numbers keep any leading zeros
            sheet.Cell(row, NameColumn).Value = item.Name;
            sheet.Cell(row, 4).Value = item.Designation;
            sheet.Cell(row, 5).Value = item.Category;
            sheet.Cell(row, DeductionColumn).Value = item.Deduction;
            sheet.Cell(row, StatusColumn).Value = "By User";
        }
        var firstData = HeaderRow + 1; var lastData = row - 1;
        if (rows.Count > 0)
        {
            sheet.Range(firstData, 1, lastData, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(firstData, FirstAmountColumn, lastData, DeductionColumn).Style.NumberFormat.Format = MoneyFormat;
        }

        var totalRow = row;
        sheet.Cell(totalRow, NameColumn).Value = "TOTAL";
        for (var column = FirstAmountColumn; column <= DeductionColumn; column++)
        {
            var letter = XLHelper.GetColumnLetterFromNumber(column);
            sheet.Cell(totalRow, column).FormulaA1 = rows.Count > 0 ? $"SUM({letter}{firstData}:{letter}{lastData})" : "0";
            sheet.Cell(totalRow, column).Style.NumberFormat.Format = TotalFormat;
        }
        sheet.Range(totalRow, 1, totalRow, lastColumn).Style.Font.Bold = true;
        sheet.Cell(totalRow, DeductionColumn).Style.Border.BottomBorder = XLBorderStyleValues.Double;

        var table = sheet.Range(HeaderRow, 1, totalRow, lastColumn);
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Dotted;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Dotted;
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        sheet.Range(totalRow, 1, totalRow, lastColumn).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // The filters in words for the title block, e.g. "Factory CONCORD APPAREL (PVT) LTD; Category EXECUTIVE".
    private async Task<string> DescribeFiltersAsync(BillReviewRowsRequest filters, CancellationToken cancellationToken)
    {
        var parts = new List<string>();
        if (await FactoryNameAsync(filters.FactoryCode, cancellationToken) is { } factory) parts.Add($"Factory {factory}");
        if (await DepartmentNameAsync(filters.DepartmentCode, cancellationToken) is { } department) parts.Add($"Department {department}");
        if (await SectionNameAsync(filters.SectionCode, cancellationToken) is { } section) parts.Add($"Section {section}");
        if (filters.CategoryCodes.Count > 0) parts.Add($"Category {string.Join(", ", await NamesAsync(filters.CategoryCodes, CategoryNameAsync, cancellationToken))}");
        if (filters.Responsibility is { } responsibility) parts.Add($"Responsibility {responsibility switch { ReviewResponsibilityFilter.ByUser => "By User", ReviewResponsibilityFilter.ByCompany => "By Company", _ => "Unassigned" }}");
        if (filters.CalculatedExcessMin is { } min) parts.Add($"Deduction from {min.ToString(MoneyFormat, CultureInfo.InvariantCulture)}");
        if (filters.CalculatedExcessMax is { } max) parts.Add($"Deduction up to {max.ToString(MoneyFormat, CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(filters.Search)) parts.Add($"Search \"{filters.Search.Trim()}\"");
        return parts.Count == 0 ? "None (all employees with a By User deduction)" : string.Join("; ", parts);
    }

    private async Task<string> FileNameSuffixAsync(BillReviewRowsRequest filters, CancellationToken cancellationToken)
    {
        // Each name is cleaned on its own; several categories are joined with "-" (as in the Monthly Bill Report file names).
        var categories = string.Join("-", (await NamesAsync(filters.CategoryCodes, CategoryNameAsync, cancellationToken)).Select(FileNamePart).Where(x => x.Length > 0));
        var parts = new[] { FileNamePart(filters.FactoryCode ?? ""), FileNamePart(await DepartmentNameAsync(filters.DepartmentCode, cancellationToken) ?? ""), FileNamePart(await SectionNameAsync(filters.SectionCode, cancellationToken) ?? ""), categories }
            .Where(x => x.Length > 0);
        return string.Concat(parts.Select(x => "_" + x));
    }

    private async Task<string?> FactoryNameAsync(string? code, CancellationToken token) => Blank(code) ? null : await db.Factories.AsNoTracking().Where(x => x.Code == code).Select(x => x.Name).FirstOrDefaultAsync(token) ?? code;
    private async Task<string?> DepartmentNameAsync(string? code, CancellationToken token) => Blank(code) ? null : await db.Departments.AsNoTracking().Where(x => x.Code == code).Select(x => x.Name).FirstOrDefaultAsync(token) ?? code;
    private async Task<string?> SectionNameAsync(string? code, CancellationToken token) => Blank(code) ? null : await db.Sections.AsNoTracking().Where(x => x.Code == code).Select(x => x.Name).FirstOrDefaultAsync(token) ?? code;
    private async Task<string?> CategoryNameAsync(string? code, CancellationToken token) => Blank(code) ? null : await db.EmployeeCategories.AsNoTracking().Where(x => x.Code == code).Select(x => x.Name).FirstOrDefaultAsync(token) ?? code;
    private static bool Blank(string? code) => string.IsNullOrWhiteSpace(code);
    private static string FileNamePart(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());
    private static async Task<List<string>> NamesAsync(IEnumerable<string> codes, Func<string?, CancellationToken, Task<string?>> name, CancellationToken token)
    {
        var names = new List<string>();
        foreach (var code in codes) names.Add(await name(code, token) ?? code);
        return names;
    }

    private sealed record DeductionRow(string Epf, string Name, string Designation, string Category, decimal Deduction);
}
