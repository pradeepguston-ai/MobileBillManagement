using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;
using MobileBill.Infrastructure.Reports;

namespace MobileBill.Infrastructure.MasterData;

// The Mobile Allocations screen as one workbook: assigned numbers, the SIM Pool and disconnected numbers, each on its own sheet.
public sealed class ClosedXmlMobileAllocationReportService(MobileBillDbContext db, IMasterDataService masterData, IClock clock) : IMobileAllocationReportService
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int HeaderRow = 5;
    private const string MoneyFormat = "#,##0.00", DateFormat = "yyyy-MM-dd";

    public static readonly string[] AllocationHeadings = ["Mobile Number", "EPF", "Name With Initials", "Calling Name", "Factory", "Department", "SIM Type", "Package", "Monthly Credit Limit", "Monthly Rental"];
    public static readonly string[] SimPoolHeadings = ["Mobile Number", "EPF", "Previous Holder", "Calling Name", "Factory", "Department", "In Pool Since", "Days in Pool", "Reason", "Monthly Credit Limit", "Monthly Rental"];
    public static readonly string[] DisconnectedHeadings = ["Mobile Number", "EPF", "Last Holder", "Calling Name", "Factory", "Department", "Package", "Disconnected On", "Reason for Disconnection"];

    public async Task<MobileAllocationReportFile> ExportAsync(CancellationToken cancellationToken)
    {
        var accounts = db.MobileAccounts.AsNoTracking();
        var assigned = (await accounts.Where(account => account.IsActive && account.Status == SimStatus.Assigned)
            .OrderBy(account => account.MobileNumber)
            .Select(account => new
            {
                account.MobileNumber, account.Employee.EPF, Name = account.Employee.NameWithInitials ?? account.Employee.FullName, account.Employee.CallingName,
                Factory = account.Employee.Factory.Name, Department = account.Employee.Department.Name, account.SimType, PackageCode = account.Package != null ? account.Package.Code : null,
                account.MonthlyCreditLimit, account.MonthlyRental,
            })
            .ToListAsync(cancellationToken))
            .Select(row => new object?[] { row.MobileNumber, row.EPF, row.Name, row.CallingName, row.Factory, row.Department, row.SimType is { } type ? SimTypeLabel(type) : null, row.PackageCode, row.MonthlyCreditLimit, row.MonthlyRental })
            .ToList();

        var pool = (await masterData.GetSimPoolAsync(cancellationToken))
            .Select(item => new object?[]
            {
                item.MobileNumber, item.PreviousEpf, item.PreviousNameWithInitials ?? item.PreviousEmployeeName, item.PreviousCallingName,
                item.Factory, item.Department, item.PooledOn, item.DaysInPool, item.Reason, item.MonthlyCreditLimit, item.MonthlyRental,
            })
            .ToList();

        // Most recently disconnected first.
        var disconnected = (await accounts.Where(account => account.Status == SimStatus.Disconnected)
            .OrderByDescending(account => account.DisconnectedOn).ThenBy(account => account.MobileNumber)
            .Select(account => new
            {
                account.MobileNumber, account.Employee.EPF, Name = account.Employee.NameWithInitials ?? account.Employee.FullName, account.Employee.CallingName,
                Factory = account.Employee.Factory.Name, Department = account.Employee.Department.Name, PackageCode = account.Package != null ? account.Package.Code : null,
                account.DisconnectedOn, account.StatusReason,
            })
            .ToListAsync(cancellationToken))
            .Select(row => new object?[] { row.MobileNumber, row.EPF, row.Name, row.CallingName, row.Factory, row.Department, row.PackageCode, row.DisconnectedOn, row.StatusReason })
            .ToList();

        using var book = new XLWorkbook();
        AddSheet(book, "Allocations", "Mobile Allocations", AllocationHeadings, assigned, "Assigned numbers");
        AddSheet(book, "SIM Pool", "SIM Pool", SimPoolHeadings, pool, "Numbers in the SIM Pool");
        AddSheet(book, "Disconnected", "Disconnected Numbers", DisconnectedHeadings, disconnected, "Disconnected numbers");
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        var date = clock.UtcNow.UtcDateTime.ToString("yyyy_MM_dd", CultureInfo.InvariantCulture);
        return new MobileAllocationReportFile($"Mobile_Allocations_{date}.xlsx", ExcelContentType, stream.ToArray());
    }

    private void AddSheet(XLWorkbook book, string name, string title, string[] headings, IReadOnlyList<object?[]> rows, string countLabel)
    {
        var sheet = book.Worksheets.Add(name);
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 10;
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = "Generated (UTC)";
        sheet.Cell(2, 2).Value = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        sheet.Cell(3, 1).Value = countLabel;
        sheet.Cell(3, 2).Value = rows.Count;
        sheet.Cell(3, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Range(2, 1, 3, 1).Style.Font.Bold = true;
        sheet.Cell(4, 1).Value = BillingReportData.SystemGeneratedNotice;
        sheet.Cell(4, 1).Style.Font.Italic = true;

        for (var column = 0; column < headings.Length; column++) sheet.Cell(HeaderRow, column + 1).Value = headings[column];
        var header = sheet.Range(HeaderRow, 1, HeaderRow, headings.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;

        for (var index = 0; index < rows.Count; index++)
        {
            for (var column = 0; column < headings.Length; column++)
            {
                var cell = sheet.Cell(HeaderRow + 1 + index, column + 1);
                switch (rows[index][column])
                {
                    case decimal amount: cell.Value = amount; cell.Style.NumberFormat.Format = MoneyFormat; break;
                    case int number: cell.Value = number; break;
                    case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); cell.Style.NumberFormat.Format = DateFormat; break;
                    // Mobile numbers and EPFs stay text so leading zeros are kept.
                    default: cell.Value = rows[index][column]?.ToString() ?? string.Empty; cell.Style.NumberFormat.Format = "@"; break;
                }
            }
        }

        var lastRow = HeaderRow + Math.Max(rows.Count, 1);
        sheet.Range(HeaderRow, 1, lastRow, headings.Length).SetAutoFilter();
        sheet.Columns(1, headings.Length).AdjustToContents(HeaderRow, lastRow, 10, 45);
        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
    }

    private static string SimTypeLabel(SimType type) => type switch { SimType.VoiceData => "Voice + Data", SimType.ESim => "eSIM", _ => type.ToString() };
}
