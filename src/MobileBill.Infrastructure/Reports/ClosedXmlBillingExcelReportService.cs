using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;
using MobileBill.Domain.Enums;
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
    private static readonly string[] Headers =
    [
        "Serial", "Mobile Phone", "EPF", "Name", "Category", "Designation", "Factory", "Department",
        "Calling Name", "Monthly Credit Limit", "Monthly Rental", "Actual Bill", "Variance", "Deduction",
        "Deduction Responsibility", "Remark"
    ];

    public async Task<BillingExcelReportResult> ExportAsync(
        BillingExcelReportRequest request,
        CancellationToken cancellationToken)
    {
        var batch = await db.BillBatches
            .AsNoTracking()
            .Include(item => item.Provider)
            .SingleOrDefaultAsync(item => item.Id == request.BatchId, cancellationToken)
            ?? throw new BillingReportNotFoundException(request.BatchId);

        if (batch.Status is not (BillBatchStatus.Completed or BillBatchStatus.Locked))
            throw new BillingReportConflictException("Excel export is allowed only for Completed or Locked bill batches.");

        if (await db.BillExceptions.AsNoTracking().AnyAsync(
                item => item.BillBatchId == request.BatchId
                    && item.Severity == BillExceptionSeverity.Blocking
                    && item.Status != BillExceptionStatus.Resolved
                    && item.Status != BillExceptionStatus.Waived,
                cancellationToken))
            throw new BillingReportConflictException("Resolve or waive all blocking exceptions before exporting the report.");

        var rows = await db.MonthlyBills
            .AsNoTracking()
            .Where(item => item.BillLine.BillBatchId == request.BatchId)
            .OrderBy(item => item.BillLine.PageNumber)
            .ThenBy(item => item.MobileNumberSnapshot)
            .Select(item => new ReportRow(
                item.Status,
                item.AssessedAt,
                item.Responsibility,
                item.EmployeeId,
                item.MobileAccountId,
                item.BillLineId,
                item.MobileNumberSnapshot,
                item.EmployeeEpfSnapshot,
                item.EmployeeNameSnapshot,
                item.CategoryNameSnapshot,
                item.DesignationNameSnapshot,
                item.FactoryNameSnapshot,
                item.DepartmentNameSnapshot,
                item.CallingNameSnapshot,

                item.CreditLimit,
                item.MonthlyRental,
                item.ActualBill,
                item.Variance,
                item.FinalDeduction,
                item.Remark))
            .ToListAsync(cancellationToken);

        var includedRows = rows.Where(item => item.Status != MonthlyBillStatus.Excluded).ToList();
        ValidateIncludedRows(includedRows);

        var exportedActualBillTotal = includedRows.Sum(item => item.ActualBill);
        var excludedActualBillTotal = rows
            .Where(item => item.Status == MonthlyBillStatus.Excluded)
            .Sum(item => item.ActualBill);
        var batchCalculatedGrandTotal = batch.CalculatedGrandTotal
            ?? throw new BillingReportConflictException("The batch calculated grand total is unavailable.");
        var difference = batchCalculatedGrandTotal - (exportedActualBillTotal + excludedActualBillTotal);
        if (difference != 0m)
            throw new BillingReportConflictException(
                $"Report reconciliation failed. Batch total {batchCalculatedGrandTotal:0.00}, "
                + $"exported total {exportedActualBillTotal:0.00}, excluded total {excludedActualBillTotal:0.00}, "
                + $"difference {difference:0.00}.");

        var content = BuildWorkbook(batch.BillingYear, batch.BillingMonth, batch.Provider.Name,
            batch.CorporateCode, includedRows, batchCalculatedGrandTotal, exportedActualBillTotal,
            excludedActualBillTotal);

        return new BillingExcelReportResult(
            batch.Id,
            batch.BillingYear,
            batch.BillingMonth,
            $"Mobile_Bill_Report_{batch.BillingYear:D4}_{batch.BillingMonth:D2}.xlsx",
            ExcelContentType,
            content,
            batchCalculatedGrandTotal,
            exportedActualBillTotal,
            excludedActualBillTotal,
            difference);
    }

    private byte[] BuildWorkbook(
        int billingYear,
        int billingMonth,
        string providerName,
        string corporateCode,
        IReadOnlyList<ReportRow> rows,
        decimal batchCalculatedGrandTotal,
        decimal exportedActualBillTotal,
        decimal excludedActualBillTotal)
    {
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
        sheet.Cell(2, 2).Value = new DateTime(billingYear, billingMonth, 1)
            .ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        sheet.Cell(3, 1).Value = "Provider";
        sheet.Cell(3, 2).Value = providerName;
        sheet.Cell(3, 4).Value = "Corporate Code";
        sheet.Cell(3, 5).Value = corporateCode;
        sheet.Cell(4, 1).Value = "Generated (UTC)";
        sheet.Cell(4, 2).Value = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        sheet.Range(2, 1, 4, 1).Style.Font.Bold = true;
        sheet.Cell(3, 4).Style.Font.Bold = true;

        for (var column = 1; column <= Headers.Length; column++)
            sheet.Cell(HeaderRow, column).Value = Headers[column - 1];

        var firstDataRow = HeaderRow + 1;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var targetRow = firstDataRow + index;
            sheet.Cell(targetRow, 1).Value = index + 1;
            sheet.Cell(targetRow, 2).Value = row.MobileNumber;
            sheet.Cell(targetRow, 3).Value = row.EmployeeEpf;
            sheet.Cell(targetRow, 4).Value = row.EmployeeName;
            sheet.Cell(targetRow, 5).Value = row.Category ?? string.Empty;
            sheet.Cell(targetRow, 6).Value = row.Designation ?? string.Empty;
            sheet.Cell(targetRow, 7).Value = row.Factory ?? string.Empty;
            sheet.Cell(targetRow, 8).Value = row.Department ?? string.Empty;
            sheet.Cell(targetRow, 9).Value = row.CallingName ?? string.Empty;
            sheet.Cell(targetRow, 10).Value = row.CreditLimit;
            sheet.Cell(targetRow, 11).Value = row.MonthlyRental;
            sheet.Cell(targetRow, 12).Value = row.ActualBill;
            sheet.Cell(targetRow, 13).Value = row.Variance;
            sheet.Cell(targetRow, 14).Value = row.FinalDeduction;
            sheet.Cell(targetRow, 15).Value = row.Responsibility == Responsibility.ByUser ? "By User" : "By Company";
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
        sheet.Cell(totalsRow, 10).Value = rows.Sum(item => item.CreditLimit);
        sheet.Cell(totalsRow, 11).Value = rows.Sum(item => item.MonthlyRental);
        sheet.Cell(totalsRow, 12).Value = exportedActualBillTotal;
        sheet.Cell(totalsRow, 13).Value = rows.Sum(item => item.Variance);
        sheet.Cell(totalsRow, 14).Value = rows.Sum(item => item.FinalDeduction);
        sheet.Range(totalsRow, 1, totalsRow, ColumnCount).Style.Font.Bold = true;
        sheet.Range(totalsRow, 1, totalsRow, ColumnCount).Style.Border.TopBorder = XLBorderStyleValues.Double;
        sheet.Range(totalsRow, 10, totalsRow, 14).Style.NumberFormat.Format = MoneyFormat;

        if (excludedActualBillTotal > 0m)
        {
            WriteReconciliationRow(sheet, totalsRow + 2, "Provider / Batch Total", batchCalculatedGrandTotal);
            WriteReconciliationRow(sheet, totalsRow + 3, "Less: Excluded Records", excludedActualBillTotal);
            WriteReconciliationRow(sheet, totalsRow + 4, "Report Actual Bill Total", exportedActualBillTotal);
            sheet.Range(totalsRow + 2, 1, totalsRow + 4, 1).Style.Font.Bold = true;
        }

        sheet.SheetView.FreezeRows(HeaderRow);
        ApplyColumnWidths(sheet);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void ValidateIncludedRows(IEnumerable<ReportRow> rows)
    {
        foreach (var row in rows)
        {
            if (row.AssessedAt is null)
                throw new BillingReportConflictException($"Monthly bill for mobile {row.MobileNumber} has not been assessed.");
            if (row.Responsibility is not (Responsibility.ByUser or Responsibility.ByCompany))
                throw new BillingReportConflictException($"Monthly bill for mobile {row.MobileNumber} has no valid responsibility.");
            if (row.EmployeeId == Guid.Empty || row.MobileAccountId == Guid.Empty || row.BillLineId == Guid.Empty
                || string.IsNullOrWhiteSpace(row.MobileNumber) || string.IsNullOrWhiteSpace(row.EmployeeEpf)
                || string.IsNullOrWhiteSpace(row.EmployeeName))
                throw new BillingReportConflictException($"Monthly bill for mobile {row.MobileNumber} is missing required historical snapshot data.");
        }
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

    private sealed record ReportRow(
        MonthlyBillStatus Status,
        DateTimeOffset? AssessedAt,
        Responsibility? Responsibility,
        Guid EmployeeId,
        Guid MobileAccountId,
        Guid BillLineId,
        string MobileNumber,
        string EmployeeEpf,
        string EmployeeName,
        string? Category,
        string? Designation,
        string? Factory,
        string? Department,
        string? CallingName,

        decimal CreditLimit,
        decimal MonthlyRental,
        decimal ActualBill,
        decimal Variance,
        decimal FinalDeduction,
        string? Remark);
}
