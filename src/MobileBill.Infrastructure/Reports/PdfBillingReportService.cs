using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;
using MobileBill.Infrastructure.Persistence;
using PdfSharp.Fonts;

namespace MobileBill.Infrastructure.Reports;

// Builds the same monthly bill report as the Excel export (same rows, totals and notice) as an A4 landscape PDF.
public sealed class PdfBillingReportService(MobileBillDbContext db, IClock clock) : IBillingPdfReportService
{
    public const string PdfContentType = "application/pdf";
    private const string FontName = "Arial";
    private const string HeaderColor = "#1F4E78";
    private const string RuleColor = "#D9E2F3";

    // Widths in millimetres for the 19 report columns; they add up to the 277mm printable width of A4 landscape.
    private static readonly double[] ColumnWidths = [7, 15, 10, 26, 14, 19, 15, 15, 16, 15, 13, 13, 12, 14, 14, 14, 15, 15, 15];
    // 0-based positions of the columns written below; they follow BillingReportData.Headers.
    private const int SectionColumn = 8, SubSectionColumn = 9, CallingNameColumn = 10, CreditLimitColumn = 11, MonthlyRentalColumn = 12,
        ActualBillColumn = 13, VarianceColumn = 14, DeductionColumn = 15, ResponsibilityColumn = 16, RemarkColumn = 17, PackageColumn = 18;
    private static readonly HashSet<int> NumericColumns = [CreditLimitColumn, MonthlyRentalColumn, ActualBillColumn, VarianceColumn, DeductionColumn];

    static PdfBillingReportService() => ReportFonts.Configure();

    public async Task<BillingExcelReportResult> ExportAsync(BillingExcelReportRequest request, CancellationToken cancellationToken)
    {
        var data = await BillingReportData.LoadAsync(db, request.BatchId, cancellationToken, request.FactoryCodes, request.CategoryCodes, request.SectionCodes);
        var content = Render(BuildDocument(data));

        return new BillingExcelReportResult(
            data.BatchId,
            data.BillingYear,
            data.BillingMonth,
            data.FileName("pdf"),
            PdfContentType,
            content,
            data.BatchCalculatedGrandTotal,
            data.ExportedActualBillTotal,
            data.ExcludedActualBillTotal,
            data.Difference);
    }

    private Document BuildDocument(BillingReportData data)
    {
        var period = new DateTime(data.BillingYear, data.BillingMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var document = new Document();
        document.Info.Title = $"Monthly Mobile Bill Report - {period}";
        document.Info.Author = "Mobile Bill Management";
        document.Info.Subject = BillingReportData.SystemGeneratedNotice;

        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontName;
        normal.Font.Size = 6.5;

        var section = document.AddSection();
        var setup = section.PageSetup;
        setup.PageFormat = PageFormat.A4;
        setup.Orientation = Orientation.Landscape;
        setup.LeftMargin = setup.RightMargin = Unit.FromMillimeter(10);
        setup.TopMargin = Unit.FromMillimeter(10);
        setup.BottomMargin = Unit.FromMillimeter(16);
        setup.FooterDistance = Unit.FromMillimeter(6);

        var title = section.AddParagraph("Monthly Mobile Bill Report");
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(4);

        var info = section.AddParagraph();
        info.Format.SpaceAfter = Unit.FromPoint(2);
        AddLabelled(info, "Billing Period: ", period);
        AddLabelled(info, "    Provider: ", data.ProviderName);
        AddLabelled(info, "    Corporate Code: ", data.CorporateCode);
        AddLabelled(info, "    Factory: ", data.FactoryLabel);
        AddLabelled(info, "    Category: ", data.CategoryLabel);
        AddLabelled(info, "    Section: ", data.SectionLabel);
        AddLabelled(info, "    Generated (UTC): ", clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

        var notice = section.AddParagraph(BillingReportData.SystemGeneratedNotice);
        notice.Format.Font.Italic = true;
        notice.Format.SpaceAfter = Unit.FromPoint(6);

        AddReportTable(section, data);
        AddSummary(section, data);

        var closing = section.AddParagraph(BillingReportData.SystemGeneratedNotice);
        closing.Format.Font.Italic = true;
        closing.Format.SpaceBefore = Unit.FromPoint(10);

        AddFooter(section);
        return document;
    }

    private static void AddLabelled(Paragraph paragraph, string label, string value)
    {
        paragraph.AddFormattedText(label, TextFormat.Bold);
        paragraph.AddText(value);
    }

    private static void AddReportTable(Section section, BillingReportData data)
    {
        var table = section.AddTable();
        table.Borders.Bottom.Width = 0.25;
        table.Borders.Bottom.Color = Color.Parse(RuleColor);
        table.Rows.LeftIndent = 0;
        table.TopPadding = table.BottomPadding = Unit.FromPoint(1.5);
        table.LeftPadding = table.RightPadding = Unit.FromPoint(2);
        foreach (var width in ColumnWidths) table.AddColumn(Unit.FromMillimeter(width));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Color.Parse(HeaderColor);
        header.Format.Font.Color = Colors.White;
        header.Format.Font.Bold = true;
        header.Format.Alignment = ParagraphAlignment.Center;
        header.VerticalAlignment = VerticalAlignment.Center;
        for (var column = 0; column < BillingReportData.Headers.Length; column++)
            header.Cells[column].AddParagraph(BillingReportData.Headers[column]);

        for (var index = 0; index < data.Entries.Count; index++)
        {
            var entry = data.Entries[index];
            var row = table.AddRow();
            row.VerticalAlignment = VerticalAlignment.Center;
            row.KeepWith = 0;
            Set(row, 0, (index + 1).ToString(CultureInfo.InvariantCulture));
            Set(row, 1, entry.MobileNumber);
            Set(row, ActualBillColumn, Money(entry.ActualBill));
            if (entry.Bill is not { } bill) continue;
            Set(row, 2, bill.EmployeeEpf);
            Set(row, 3, bill.EmployeeName);
            Set(row, 4, bill.Category);
            Set(row, 5, bill.Designation);
            Set(row, 6, bill.Factory);
            Set(row, 7, bill.Department);
            Set(row, SectionColumn, bill.Section);
            Set(row, SubSectionColumn, bill.SubSection);
            Set(row, CallingNameColumn, bill.CallingName);
            Set(row, CreditLimitColumn, Money(bill.CreditLimit));
            Set(row, MonthlyRentalColumn, Money(bill.MonthlyRental));
            Set(row, VarianceColumn, Money(bill.Variance));
            Set(row, DeductionColumn, Money(bill.DisplayedDeduction));
            Set(row, ResponsibilityColumn, bill.ResponsibilityLabel);
            Set(row, RemarkColumn, bill.Remark);
            Set(row, PackageColumn, bill.PackageCode);
        }

        var totals = table.AddRow();
        totals.Format.Font.Bold = true;
        totals.Borders.Top.Width = 1;
        totals.Borders.Top.Style = BorderStyle.Single;
        Set(totals, 0, "Totals");
        Set(totals, CreditLimitColumn, Money(data.Entries.Sum(item => item.Bill?.CreditLimit ?? 0m)));
        Set(totals, MonthlyRentalColumn, Money(data.Entries.Sum(item => item.Bill?.MonthlyRental ?? 0m)));
        Set(totals, ActualBillColumn, Money(data.ExportedActualBillTotal));
        Set(totals, VarianceColumn, Money(data.Entries.Sum(item => item.Bill?.Variance ?? 0m)));
        Set(totals, DeductionColumn, Money(data.TotalDisplayedDeduction));
    }

    private static void AddSummary(Section section, BillingReportData data)
    {
        var lines = new List<(string Label, decimal Amount)>();
        if (data.ExcludedActualBillTotal > 0m)
        {
            lines.Add(("Provider / Batch Total", data.BatchCalculatedGrandTotal));
            lines.Add(("Less: Excluded Records", data.ExcludedActualBillTotal));
            lines.Add(("Report Actual Bill Total", data.ExportedActualBillTotal));
        }
        lines.Add(("Deducted from Employees (By User)", data.DeductedFromEmployees));
        lines.Add(("Borne by Company (By Company)", data.BorneByCompany));
        if (data.SimPoolCost > 0m) lines.Add(("SIM Pool cost (numbers with no holder)", data.SimPoolCost));

        var spacer = section.AddParagraph();
        spacer.Format.SpaceAfter = Unit.FromPoint(6);
        var table = section.AddTable();
        table.TopPadding = table.BottomPadding = Unit.FromPoint(1.5);
        table.AddColumn(Unit.FromMillimeter(70));
        table.AddColumn(Unit.FromMillimeter(28));
        foreach (var (label, amount) in lines)
        {
            var row = table.AddRow();
            row.Format.Font.Bold = true;
            row.Cells[0].AddParagraph(label);
            var value = row.Cells[1].AddParagraph(Money(amount));
            value.Format.Alignment = ParagraphAlignment.Right;
        }
    }

    private static void AddFooter(Section section)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 6.5;
        footer.Format.Font.Color = Color.Parse("#555555");
        footer.AddText(BillingReportData.SystemGeneratedNotice + "   |   Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();
    }

    private static void Set(Row row, int column, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var paragraph = row.Cells[column].AddParagraph(text);
        if (NumericColumns.Contains(column)) paragraph.Format.Alignment = ParagraphAlignment.Right;
    }

    private static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static byte[] Render(Document document)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }
}

// PDFsharp needs fonts it can read. Windows uses the installed fonts; elsewhere the common Linux system fonts are used.
internal static class ReportFonts
{
    private static readonly object Gate = new();
    private static bool configured;

    public static void Configure()
    {
        lock (Gate)
        {
            if (configured) return;
            configured = true;
            if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            else GlobalFontSettings.FontResolver = new SystemFontResolver();
        }
    }

    private sealed class SystemFontResolver : IFontResolver
    {
        private static readonly string[] Directories = ["/usr/share/fonts", "/usr/local/share/fonts", "/Library/Fonts", "/System/Library/Fonts"];
        private static readonly string[] Regular = ["LiberationSans-Regular.ttf", "DejaVuSans.ttf", "Arial.ttf"];
        private static readonly string[] Bold = ["LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf", "Arial Bold.ttf"];
        private static readonly string[] Italic = ["LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf", "Arial Italic.ttf"];
        private static readonly string[] BoldItalic = ["LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf", "Arial Bold Italic.ttf"];

        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
            new(bold ? (italic ? "bolditalic" : "bold") : (italic ? "italic" : "regular"));

        public byte[]? GetFont(string faceName)
        {
            var candidates = faceName switch { "bolditalic" => BoldItalic, "bold" => Bold, "italic" => Italic, _ => Regular };
            foreach (var directory in Directories.Where(Directory.Exists))
                foreach (var name in candidates)
                {
                    var path = Directory.EnumerateFiles(directory, name, SearchOption.AllDirectories).FirstOrDefault();
                    if (path is not null) return File.ReadAllBytes(path);
                }
            throw new InvalidOperationException("No usable system font was found for the PDF report. Install the Liberation Sans or DejaVu Sans fonts.");
        }
    }
}
