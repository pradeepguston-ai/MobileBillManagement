using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Reports;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Reports;

// The monthly bill report content, loaded and validated once so the Excel and PDF exports always show the same data.
internal sealed record BillingReportData(
    Guid BatchId,
    int BillingYear,
    int BillingMonth,
    string ProviderName,
    string CorporateCode,
    IReadOnlyList<ReportEntry> Entries,
    decimal BatchCalculatedGrandTotal,
    decimal ExportedActualBillTotal,
    decimal ExcludedActualBillTotal,
    decimal Difference,
    IReadOnlyList<ReportFilterValue>? Factories = null,
    IReadOnlyList<ReportFilterValue>? Categories = null,
    IReadOnlyList<ReportFilterValue>? Sections = null)
{
    public const string SystemGeneratedNotice = "This is a system generated report. No signature required.";

    public static readonly string[] Headers =
    [
        "Serial", "Mobile Phone", "EPF", "Name", "Category", "Designation", "Factory", "Department",
        "Section", "Sub Section", "Calling Name", "Monthly Credit Limit", "Monthly Rental", "Actual Bill", "Variance", "Deduction",
        "Deduction Responsibility", "Remark", "Package"
    ];

    // By User rows show the deduction taken from the employee; By Company rows show the excess the company absorbs.
    public decimal TotalDisplayedDeduction => Entries.Sum(entry => entry.Bill?.DisplayedDeduction ?? 0m);
    public decimal DeductedFromEmployees => Entries.Sum(entry => entry.Bill is { Responsibility: Responsibility.ByUser } bill ? bill.FinalDeduction : 0m);
    public decimal BorneByCompany => Entries.Sum(entry => entry.Bill is { Responsibility: Responsibility.ByCompany } bill ? bill.CalculatedExcess : 0m);
    // The full bills of numbers with no holder (SIM Pool, or billed after disconnection): the cost of idle SIMs.
    public decimal SimPoolCost => Entries.Sum(entry => entry.Bill is { IsPooled: true } bill ? bill.ActualBill : 0m);

    public bool IsFiltered => Factories is { Count: > 0 } || Categories is { Count: > 0 } || Sections is { Count: > 0 };

    public string FactoryLabel => Label(Factories, "All factories");
    public string CategoryLabel => Label(Categories, "All categories");
    public string SectionLabel => Label(Sections, "All sections");

    private static string Label(IReadOnlyList<ReportFilterValue>? values, string all) =>
        values is { Count: > 0 } ? string.Join(", ", values.Select(value => $"{value.Name} ({value.Code})")) : all;

    private static string Suffix(IReadOnlyList<ReportFilterValue>? values) =>
        values is { Count: > 0 } ? "_" + string.Join("-", values.Select(value => value.Code)) : string.Empty;

    public string FileName(string extension) =>
        $"Mobile_Bill_Report_{BillingYear:D4}_{BillingMonth:D2}{Suffix(Factories)}{Suffix(Categories)}{Suffix(Sections)}.{extension}";

    public static async Task<BillingReportData> LoadAsync(MobileBillDbContext db, Guid batchId, CancellationToken cancellationToken, IReadOnlyCollection<string>? factoryCodes = null, IReadOnlyCollection<string>? categoryCodes = null, IReadOnlyCollection<string>? sectionCodes = null)
    {
        var batch = await db.BillBatches
            .AsNoTracking()
            .Include(item => item.Provider)
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
            ?? throw new BillingReportNotFoundException(batchId);

        if (batch.Status is not (BillBatchStatus.Completed or BillBatchStatus.Locked))
            throw new BillingReportConflictException("Excel export is allowed only for Completed or Locked bill batches.");

        var rows = await db.MonthlyBills
            .AsNoTracking()
            .Where(item => item.BillLine.BillBatchId == batchId)
            .OrderBy(item => item.BillLine.PageNumber)
            .ThenBy(item => item.MobileNumberSnapshot)
            .Select(item => new ReportRow(
                item.FactoryCodeSnapshot,
                item.CategoryCodeSnapshot,
                item.SectionCodeSnapshot,
                item.BillLine.PageNumber,
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
                item.SectionNameSnapshot,
                item.SubSectionNameSnapshot,
                item.CallingNameSnapshot,
                item.CreditLimit,
                item.MonthlyRental,
                item.ActualBill,
                item.Variance,
                item.CalculatedExcess,
                item.FinalDeduction,
                item.Remark,
                item.IsPooled,
                item.PackageCodeSnapshot))
            .ToListAsync(cancellationToken);

        var includedRows = rows.Where(item => item.Status != MonthlyBillStatus.Excluded).ToList();
        ValidateIncludedRows(includedRows);

        // Extracted lines that never became a monthly bill (unresolved exceptions such as an unknown
        // mobile number) stay in the report with the employee/allocation columns left blank.
        var exceptionLines = await db.BillLines
            .AsNoTracking()
            .Where(line => line.BillBatchId == batchId
                && line.ExtractionStatus == BillLineExtractionStatus.Extracted
                && line.MonthlyBill == null)
            .Select(line => new { line.PageNumber, line.MobileNumber, line.TotalDueAmount })
            .ToListAsync(cancellationToken);
        var entries = includedRows.Select(row => new ReportEntry(row.PageNumber, row.MobileNumber, row.ActualBill, row))
            .Concat(exceptionLines.Select(line => new ReportEntry(line.PageNumber, line.MobileNumber, line.TotalDueAmount, null)))
            .OrderBy(entry => entry.PageNumber).ThenBy(entry => entry.MobileNumber, StringComparer.Ordinal)
            .ToList();

        var exportedActualBillTotal = entries.Sum(entry => entry.ActualBill);
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

        var full = new BillingReportData(
            batch.Id, batch.BillingYear, batch.BillingMonth, batch.Provider.Name, batch.CorporateCode,
            entries, batchCalculatedGrandTotal, exportedActualBillTotal, excludedActualBillTotal, difference);
        var factories = Normalize(factoryCodes);
        var categories = Normalize(categoryCodes);
        var sections = Normalize(sectionCodes);
        if (factories.Count == 0 && categories.Count == 0 && sections.Count == 0) return full;

        // A filtered report is cut from the full, already reconciled batch report. Lines without a monthly
        // bill (unresolved exceptions) have no factory, category or section, so they appear only in the full report.
        var factoryNames = await db.Factories.AsNoTracking()
            .Where(item => factories.Contains(item.Code))
            .ToDictionaryAsync(item => item.Code, item => item.Name, cancellationToken);
        var categoryNames = await db.EmployeeCategories.AsNoTracking()
            .Where(item => categories.Contains(item.Code))
            .ToDictionaryAsync(item => item.Code, item => item.Name, cancellationToken);
        var sectionNames = await db.Sections.AsNoTracking()
            .Where(item => sections.Contains(item.Code))
            .ToDictionaryAsync(item => item.Code, item => item.Name, cancellationToken);

        var filtered = entries
            .Where(entry => entry.Bill is { } bill
                && (factories.Count == 0 || factories.Contains(bill.FactoryCode))
                && (categories.Count == 0 || categories.Contains(bill.CategoryCode))
                && (sections.Count == 0 || (bill.SectionCode is not null && sections.Contains(bill.SectionCode))))
            .ToList();
        return full with
        {
            Entries = filtered,
            ExportedActualBillTotal = filtered.Sum(entry => entry.ActualBill),
            ExcludedActualBillTotal = 0m,
            Factories = Resolve("Factory", factories, factoryNames),
            Categories = Resolve("Category", categories, categoryNames),
            Sections = Resolve("Section", sections, sectionNames),
        };
    }

    private static List<string> Normalize(IReadOnlyCollection<string>? codes) =>
        (codes ?? []).Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()).Distinct(StringComparer.Ordinal).ToList();

    private static List<ReportFilterValue> Resolve(string kind, IEnumerable<string> codes, IReadOnlyDictionary<string, string> names) =>
        codes.Select(code => names.TryGetValue(code, out var name)
            ? new ReportFilterValue(code, name)
            : throw new BillingReportFilterNotFoundException(kind, code)).ToList();

    private static void ValidateIncludedRows(IReadOnlyCollection<ReportRow> rows)
    {
        // Name every unassessed number at once (for example bills added later by resolving exceptions) so they can be fixed together.
        var unassessed = rows.Where(row => row.AssessedAt is null).Select(row => row.MobileNumber).ToList();
        if (unassessed.Count == 1)
            throw new BillingReportConflictException($"Monthly bill for mobile {unassessed[0]} has not been assessed. Assign its responsibility on the Monthly Bill Review screen, then download the report again.");
        if (unassessed.Count > 1)
            throw new BillingReportConflictException($"{unassessed.Count} monthly bills have not been assessed: {string.Join(", ", unassessed)}. Filter the Monthly Bill Review screen by Unassessed, assign their responsibility, then download the report again.");
        foreach (var row in rows)
        {
            if (row.Responsibility is not (Responsibility.ByUser or Responsibility.ByCompany))
                throw new BillingReportConflictException($"Monthly bill for mobile {row.MobileNumber} has no valid responsibility.");
            if (row.EmployeeId == Guid.Empty || row.MobileAccountId == Guid.Empty || row.BillLineId == Guid.Empty
                || string.IsNullOrWhiteSpace(row.MobileNumber) || string.IsNullOrWhiteSpace(row.EmployeeEpf)
                || string.IsNullOrWhiteSpace(row.EmployeeName))
                throw new BillingReportConflictException($"Monthly bill for mobile {row.MobileNumber} is missing required historical snapshot data.");
        }
    }
}

internal sealed record ReportFilterValue(string Code, string Name);

internal sealed record ReportEntry(int PageNumber, string MobileNumber, decimal ActualBill, ReportRow? Bill);

internal sealed record ReportRow(
    string FactoryCode,
    string CategoryCode,
    string? SectionCode,
    int PageNumber,
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
    string? Section,
    string? SubSection,
    string? CallingName,
    decimal CreditLimit,
    decimal MonthlyRental,
    decimal ActualBill,
    decimal Variance,
    decimal CalculatedExcess,
    decimal FinalDeduction,
    string? Remark,
    bool IsPooled = false,
    string? PackageCode = null)
{
    public decimal DisplayedDeduction => Responsibility == Domain.Enums.Responsibility.ByCompany ? CalculatedExcess : FinalDeduction;
    public string ResponsibilityLabel => Responsibility == Domain.Enums.Responsibility.ByUser ? "By User" : "By Company";
}
