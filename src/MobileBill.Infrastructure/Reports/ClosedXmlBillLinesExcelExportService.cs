using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;
using MobileBill.Domain.Entities;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Reports;

public sealed class ClosedXmlBillLinesExcelExportService(MobileBillDbContext db, IClock clock) : IBillLinesExcelExportService
{
    private const string WorksheetName = "Extracted Bill Lines";
    private const int HeaderRow = 6;
    private const string MoneyFormat = "#,##0.00";

    // Same columns, in the same order, as the Extracted Bill Lines screen.
    private static readonly (string Header, Func<BillLine, decimal> Value)[] MoneyColumns =
    [
        ("Previous Due", line => line.PreviousDueAmount),
        ("Payments", line => line.Payments),
        ("Total Usage", line => line.TotalUsageCharges),
        ("IDD", line => line.Idd),
        ("Roaming", line => line.Roaming),
        ("VAS", line => line.ValueAddedServices),
        ("Discounts", line => line.Discounts),
        ("Bill Adjustments", line => line.BillAdjustmentsBalanceTransfers),
        ("Commitment Charges", line => line.CommitmentCharges),
        ("Late Payment Charges", line => line.LatePaymentCharges),
        ("Add To Bill", line => line.AddToBill),
        ("Instalment Plans", line => line.InstalmentPlans),
        ("Government Taxes / Levies", line => line.GovernmentTaxesAndLevies),
        ("VAT", line => line.Vat),
        ("Charges For Bill Period", line => line.ChargesForBillPeriod),
        ("Total Due", line => line.TotalDueAmount),
    ];

    public async Task<BillLinesExcelExportResult> ExportAsync(Guid batchId, string? search, CancellationToken cancellationToken)
    {
        var batch = await db.BillBatches.AsNoTracking().Include(item => item.Provider)
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
            ?? throw new BillBatchNotFoundException(batchId);

        var query = db.BillLines.AsNoTracking().Where(line => line.BillBatchId == batchId);
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term)) query = query.Where(line => line.MobileNumber.Contains(term));
        var lines = await query.OrderBy(line => line.PageNumber).ThenBy(line => line.MobileNumber).ToListAsync(cancellationToken);

        var content = BuildWorkbook(batch, lines, term);
        var fileName = $"Extracted_Bill_Lines_{batch.BillingYear:D4}_{batch.BillingMonth:D2}.xlsx";
        return new BillLinesExcelExportResult(fileName, ClosedXmlBillingExcelReportService.ExcelContentType, content, lines.Count);
    }

    private byte[] BuildWorkbook(BillBatch batch, IReadOnlyList<BillLine> lines, string? search)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(WorksheetName);
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 10;

        var headers = new List<string> { "Serial", "Mobile Account" };
        headers.AddRange(MoneyColumns.Select(column => column.Header));
        headers.AddRange(["Extraction Status", "Page Number", "Extraction Error"]);
        var columnCount = headers.Count;
        var firstMoneyColumn = 3;
        var lastMoneyColumn = firstMoneyColumn + MoneyColumns.Length - 1;

        sheet.Cell(1, 1).Value = "Extracted Bill Lines";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = "Billing Period";
        sheet.Cell(2, 2).Value = new DateTime(batch.BillingYear, batch.BillingMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        sheet.Cell(3, 1).Value = "Provider";
        sheet.Cell(3, 2).Value = batch.Provider.Name;
        sheet.Cell(3, 4).Value = "Corporate Code";
        sheet.Cell(3, 5).Value = batch.CorporateCode;
        sheet.Cell(4, 1).Value = "Generated (UTC)";
        sheet.Cell(4, 2).Value = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        sheet.Cell(4, 4).Value = "Search";
        sheet.Cell(4, 5).Value = string.IsNullOrEmpty(search) ? "All lines" : search;
        sheet.Range(2, 1, 4, 1).Style.Font.Bold = true;
        sheet.Range(3, 4, 4, 4).Style.Font.Bold = true;
        sheet.Cell(5, 1).Value = BillingReportData.SystemGeneratedNotice;
        sheet.Cell(5, 1).Style.Font.Italic = true;

        for (var column = 1; column <= columnCount; column++)
            sheet.Cell(HeaderRow, column).Value = headers[column - 1];

        var firstDataRow = HeaderRow + 1;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var row = firstDataRow + index;
            sheet.Cell(row, 1).Value = index + 1;
            sheet.Cell(row, 2).Value = line.MobileNumber;
            for (var money = 0; money < MoneyColumns.Length; money++)
                sheet.Cell(row, firstMoneyColumn + money).Value = MoneyColumns[money].Value(line);
            sheet.Cell(row, lastMoneyColumn + 1).Value = line.ExtractionStatus.ToString();
            sheet.Cell(row, lastMoneyColumn + 2).Value = line.PageNumber;
            sheet.Cell(row, lastMoneyColumn + 3).Value = line.ExtractionError ?? string.Empty;
            // Lines that failed extraction are tinted, as they are on screen.
            if (line.ExtractionStatus != Domain.Enums.BillLineExtractionStatus.Extracted)
                sheet.Range(row, 1, row, columnCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#FBE9E9");
        }

        var lastDataRow = HeaderRow + lines.Count;
        var table = sheet.Range(HeaderRow, 1, Math.Max(lastDataRow, firstDataRow), columnCount).CreateTable("ExtractedBillLinesTable");
        if (lines.Count == 0) table.DataRange?.Clear(XLClearOptions.Contents);
        table.Theme = XLTableTheme.None;
        table.ShowAutoFilter = true;

        var header = sheet.Range(HeaderRow, 1, HeaderRow, columnCount);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;
        header.Style.Alignment.WrapText = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(HeaderRow).Height = 32;

        if (lines.Count > 0)
        {
            sheet.Range(firstDataRow, firstMoneyColumn, lastDataRow, lastMoneyColumn).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(firstDataRow, 2, lastDataRow, 2).Style.NumberFormat.Format = "@";
        }

        var totalsRow = lastDataRow + 1;
        sheet.Cell(totalsRow, 1).Value = "Totals";
        for (var money = 0; money < MoneyColumns.Length; money++)
            sheet.Cell(totalsRow, firstMoneyColumn + money).Value = lines.Sum(MoneyColumns[money].Value);
        sheet.Range(totalsRow, 1, totalsRow, columnCount).Style.Font.Bold = true;
        sheet.Range(totalsRow, 1, totalsRow, columnCount).Style.Border.TopBorder = XLBorderStyleValues.Double;
        sheet.Range(totalsRow, firstMoneyColumn, totalsRow, lastMoneyColumn).Style.NumberFormat.Format = MoneyFormat;

        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Column(1).Width = 8;
        sheet.Column(2).Width = 16;
        for (var column = firstMoneyColumn; column <= lastMoneyColumn; column++) sheet.Column(column).Width = 14;
        sheet.Column(lastMoneyColumn + 1).Width = 16;
        sheet.Column(lastMoneyColumn + 2).Width = 12;
        sheet.Column(lastMoneyColumn + 3).Width = 40;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);
        sheet.PageSetup.Footer.Center.AddText(BillingReportData.SystemGeneratedNotice);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
