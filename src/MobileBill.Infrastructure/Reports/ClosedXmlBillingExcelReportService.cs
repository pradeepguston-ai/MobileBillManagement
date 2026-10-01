using System.Globalization;
using ClosedXML.Excel;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Reports;

public sealed class ClosedXmlBillingExcelReportService(MobileBillDbContext db, IClock clock)
    : IBillingExcelReportService
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string WorksheetName = "Monthly Bill Report";
    private const int HeaderRow = 5;
    private const int ColumnCount = 16;
    private const string MoneyFormat = "#,##0.00";

    public async Task<BillingExcelReportResult> ExportAsync(
        BillingExcelReportRequest request,
        CancellationToken cancellationToken)
    {
        var data = await BillingReportData.LoadAsync(db, request.BatchId, cancellationToken, request.FactoryCodes, request.CategoryCodes);
        var content = BuildWorkbook(data);

        return new BillingExcelReportResult(
            data.BatchId,
            data.BillingYear,
            data.BillingMonth,
            data.FileName("xlsx"),
            ExcelContentType,
            content,
            data.BatchCalculatedGrandTotal,
            data.ExportedActualBillTotal,
            data.ExcludedActualBillTotal,
            data.Difference);
    }

    private byte[] BuildWorkbook(BillingReportData data)
    {
        var rows = data.Entries;
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(WorksheetName);
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 10;

        sheet.Range(1, 1, 1, ColumnCount).Merge();
        sheet.Cell(1, 1).Value = "Monthly Mobile Bill Report";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        sheet.Cell(2, 1).Value = "Billing Period";
        sheet.Cell(2, 2).Value = new DateTime(data.BillingYear, data.BillingMonth, 1)
            .ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        sheet.Cell(3, 1).Value = "Provider";
        sheet.Cell(3, 2).Value = data.ProviderName;
        sheet.Cell(3, 4).Value = "Corporate Code";
        sheet.Cell(3, 5).Value = data.CorporateCode;
        sheet.Cell(4, 1).Value = "Generated (UTC)";
        sheet.Cell(4, 2).Value = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        sheet.Range(2, 1, 4, 1).Style.Font.Bold = true;
        sheet.Cell(3, 4).Style.Font.Bold = true;
        sheet.Cell(2, 4).Value = "Factory";
        sheet.Cell(2, 5).Value = data.FactoryLabel;
        sheet.Cell(2, 4).Style.Font.Bold = true;
        sheet.Cell(2, 7).Value = "Category";
        sheet.Cell(2, 8).Value = data.CategoryLabel;
        sheet.Cell(2, 7).Style.Font.Bold = true;
        sheet.Cell(4, 4).Value = BillingReportData.SystemGeneratedNotice;
        sheet.Cell(4, 4).Style.Font.Italic = true;

        for (var column = 1; column <= BillingReportData.Headers.Length; column++)
            sheet.Cell(HeaderRow, column).Value = BillingReportData.Headers[column - 1];

        var firstDataRow = HeaderRow + 1;
        for (var index = 0; index < rows.Count; index++)
        {
            var entry = rows[index];
            var targetRow = firstDataRow + index;
            sheet.Cell(targetRow, 1).Value = index + 1;
            sheet.Cell(targetRow, 2).Value = entry.MobileNumber;
            sheet.Cell(targetRow, 12).Value = entry.ActualBill;
            if (entry.Bill is not { } row) continue;
            sheet.Cell(targetRow, 3).Value = row.EmployeeEpf;
            sheet.Cell(targetRow, 4).Value = row.EmployeeName;
            sheet.Cell(targetRow, 5).Value = row.Category ?? string.Empty;
            sheet.Cell(targetRow, 6).Value = row.Designation ?? string.Empty;
            sheet.Cell(targetRow, 7).Value = row.Factory ?? string.Empty;
            sheet.Cell(targetRow, 8).Value = row.Department ?? string.Empty;
            sheet.Cell(targetRow, 9).Value = row.CallingName ?? string.Empty;
            sheet.Cell(targetRow, 10).Value = row.CreditLimit;
            sheet.Cell(targetRow, 11).Value = row.MonthlyRental;
            sheet.Cell(targetRow, 13).Value = row.Variance;
            sheet.Cell(targetRow, 14).Value = row.DisplayedDeduction;
            sheet.Cell(targetRow, 15).Value = row.ResponsibilityLabel;
            sheet.Cell(targetRow, 16).Value = row.Remark ?? string.Empty;
        }

        var lastDataRow = HeaderRow + rows.Count;
        var table = sheet.Range(HeaderRow, 1, Math.Max(lastDataRow, firstDataRow), ColumnCount)
            .CreateTable("MonthlyBillReportTable");
        if (rows.Count == 0)
            table.DataRange?.Clear(XLClearOptions.Contents);
        table.Theme = XLTableTheme.None;
        table.ShowAutoFilter = true;

        var header = sheet.Range(HeaderRow, 1, HeaderRow, ColumnCount);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Alignment.WrapText = true;
        sheet.Row(HeaderRow).Height = 36;

        if (rows.Count > 0)
        {
            var body = sheet.Range(firstDataRow, 1, lastDataRow, ColumnCount);
            body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            body.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            body.Style.Border.BottomBorderColor = XLColor.FromHtml("#D9E2F3");
            sheet.Range(firstDataRow, 10, lastDataRow, 14).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(firstDataRow, 2, lastDataRow, 3).Style.NumberFormat.Format = "@";
        }

        var totalsRow = lastDataRow + 1;
        sheet.Cell(totalsRow, 1).Value = "Totals";
        sheet.Cell(totalsRow, 10).Value = rows.Sum(item => item.Bill?.CreditLimit ?? 0m);
        sheet.Cell(totalsRow, 11).Value = rows.Sum(item => item.Bill?.MonthlyRental ?? 0m);
        sheet.Cell(totalsRow, 12).Value = data.ExportedActualBillTotal;
        sheet.Cell(totalsRow, 13).Value = rows.Sum(item => item.Bill?.Variance ?? 0m);
        sheet.Cell(totalsRow, 14).Value = data.TotalDisplayedDeduction;
        sheet.Range(totalsRow, 1, totalsRow, ColumnCount).Style.Font.Bold = true;
        sheet.Range(totalsRow, 1, totalsRow, ColumnCount).Style.Border.TopBorder = XLBorderStyleValues.Double;
        sheet.Range(totalsRow, 10, totalsRow, 14).Style.NumberFormat.Format = MoneyFormat;

        if (data.ExcludedActualBillTotal > 0m)
        {
            WriteReconciliationRow(sheet, totalsRow + 2, "Provider / Batch Total", data.BatchCalculatedGrandTotal);
            WriteReconciliationRow(sheet, totalsRow + 3, "Less: Excluded Records", data.ExcludedActualBillTotal);
            WriteReconciliationRow(sheet, totalsRow + 4, "Report Actual Bill Total", data.ExportedActualBillTotal);
            sheet.Range(totalsRow + 2, 1, totalsRow + 4, 1).Style.Font.Bold = true;
        }

        // The Final Deduction column also lists what the company absorbs on By Company rows, so the
        // split between the two is spelled out below the totals.
        var splitRow = data.ExcludedActualBillTotal > 0m ? totalsRow + 6 : totalsRow + 2;
        WriteDeductionSplitRow(sheet, splitRow, "Deducted from Employees (By User)", data.DeductedFromEmployees);
        WriteDeductionSplitRow(sheet, splitRow + 1, "Borne by Company (By Company)", data.BorneByCompany);
        sheet.Range(splitRow, 1, splitRow + 1, 1).Style.Font.Bold = true;

        sheet.Cell(splitRow + 3, 1).Value = BillingReportData.SystemGeneratedNotice;
        sheet.Cell(splitRow + 3, 1).Style.Font.Italic = true;

        sheet.SheetView.FreezeRows(HeaderRow);
        ApplyColumnWidths(sheet);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);
        sheet.PageSetup.Footer.Center.AddText(BillingReportData.SystemGeneratedNotice);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteDeductionSplitRow(IXLWorksheet sheet, int row, string label, decimal amount)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 14).Value = amount;
        sheet.Cell(row, 14).Style.NumberFormat.Format = MoneyFormat;
    }

    private static void WriteReconciliationRow(IXLWorksheet sheet, int row, string label, decimal amount)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 12).Value = amount;
        sheet.Cell(row, 12).Style.NumberFormat.Format = MoneyFormat;
    }

    private static void ApplyColumnWidths(IXLWorksheet sheet)
    {
        var widths = new[] { 9d, 16d, 12d, 30d, 22d, 28d, 14d, 20d, 16d, 17d, 15d, 15d, 15d, 15d, 22d, 28d };
        for (var column = 1; column <= widths.Length; column++)
            sheet.Column(column).Width = widths[column - 1];
    }
}
