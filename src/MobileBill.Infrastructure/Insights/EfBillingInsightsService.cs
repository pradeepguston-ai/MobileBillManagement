using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Application.Insights;
using MobileBill.Domain.Calculations;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Billing;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Insights;

public sealed class EfBillingInsightsService(MobileBillDbContext db, IClock clock) : IBillingInsightsService
{
    private const int TopAccounts = 10;
    private const int MaxTrendMonths = 24;
    private const int RepeatAccountsShown = 20;

    public async Task<BillingInsightsDto> GetAsync(Guid? batchId, int months, CancellationToken cancellationToken)
    {
        // Only batches that have been matched have monthly bills to analyse.
        var options = await db.BillBatches.AsNoTracking()
            .Where(batch => db.MonthlyBills.Any(bill => bill.BillLine.BillBatchId == batch.Id))
            .OrderByDescending(batch => batch.BillingYear).ThenByDescending(batch => batch.BillingMonth).ThenByDescending(batch => batch.CreatedAtUtc)
            .Select(batch => new InsightsBatchOption(batch.Id, batch.BillingYear, batch.BillingMonth, batch.Provider.Name, batch.Status,
                batch.Status == BillBatchStatus.Completed || batch.Status == BillBatchStatus.Locked))
            .ToListAsync(cancellationToken);

        var selected = batchId is null ? options.FirstOrDefault() : options.SingleOrDefault(option => option.Id == batchId)
            ?? throw new BillBatchNotFoundException(batchId.Value);
        if (selected is null)
            return new BillingInsightsDto(options, null, null, null, null, [], [], new InsightsGroups([], [], []), [], [], [],
                new InsightsRepeatOverLimit(InsightsRules.RepeatWindowMonths, InsightsRules.RepeatOverLimitMonths, 0, []), await BuildAssetsAsync(cancellationToken));

        // Comparisons use approved figures only: the latest approved batch before the selected period.
        var previousBatch = options.FirstOrDefault(option => option.IsApproved && Period(option) < Period(selected));
        var trendBatches = options
            .Where(option => option.IsApproved && Period(option) <= Period(selected))
            .Take(Math.Clamp(months, 1, MaxTrendMonths))
            .ToList();
        if (!selected.IsApproved) trendBatches.Insert(0, selected);
        trendBatches.Reverse();

        var bills = await LoadBillsAsync([selected.Id], cancellationToken);
        var lines = await db.BillLines.AsNoTracking()
            .Where(line => line.BillBatchId == selected.Id && line.ExtractionStatus == BillLineExtractionStatus.Extracted)
            .ToListAsync(cancellationToken);
        // The repeat over-limit window: approved batches (and the selected one) in the last six billing months.
        var windowBatches = options
            .Where(option => (option.IsApproved || option.Id == selected.Id) && Period(option) <= Period(selected) && Period(option) > Period(selected) - InsightsRules.RepeatWindowMonths)
            .ToList();
        var comparisonIds = trendBatches.Select(option => option.Id).Concat(windowBatches.Select(option => option.Id)).Append(previousBatch?.Id ?? Guid.Empty)
            .Where(id => id != Guid.Empty && id != selected.Id).Distinct().ToList();
        var otherBills = await LoadBillsAsync(comparisonIds, cancellationToken);
        var roamingByBatch = await db.BillLines.AsNoTracking()
            .Where(line => (comparisonIds.Contains(line.BillBatchId) || line.BillBatchId == selected.Id) && line.ExtractionStatus == BillLineExtractionStatus.Extracted)
            .GroupBy(line => line.BillBatchId)
            .Select(group => new { BatchId = group.Key, Roaming = group.Sum(line => line.Roaming) })
            .ToDictionaryAsync(item => item.BatchId, item => item.Roaming, cancellationToken);

        var allBills = bills.Concat(otherBills).ToLookup(bill => bill.BatchId);
        InsightsKpis Kpis(Guid id) => BuildKpis(allBills[id].ToList(), roamingByBatch.GetValueOrDefault(id));

        return new BillingInsightsDto(
            options,
            selected,
            Kpis(selected.Id),
            previousBatch is null ? null : Kpis(previousBatch.Id),
            previousBatch,
            BuildChargeMix(lines),
            BuildExcessSplit(bills),
            new InsightsGroups(
                Group(bills, bill => bill.FactoryCode, bill => bill.FactoryName),
                Group(bills, bill => bill.DepartmentCode, bill => bill.DepartmentName),
                Group(bills, bill => bill.CategoryCode, bill => bill.CategoryName)),
            bills.Where(bill => bill.CalculatedExcess > 0m)
                .OrderByDescending(bill => bill.CalculatedExcess).ThenBy(bill => bill.MobileNumber, StringComparer.Ordinal)
                .Take(TopAccounts)
                .Select(bill => new InsightsTopAccount(bill.MobileNumber, bill.Epf, bill.EmployeeName, bill.CallingName, bill.FactoryName, bill.DepartmentName, bill.ActualBill, bill.Entitlement, bill.CalculatedExcess, bill.Responsibility, bill.Remark))
                .ToList(),
            trendBatches.Select(option =>
            {
                var kpis = Kpis(option.Id);
                // The part of a By User excess that a deduction override let off.
                var waived = allBills[option.Id].Where(bill => bill.Responsibility == Responsibility.ByUser).Sum(bill => bill.CalculatedExcess - bill.FinalDeduction);
                return new InsightsTrendPoint(option.Id, option.BillingYear, option.BillingMonth, option.IsApproved, kpis.TotalActualBill, kpis.TotalCalculatedExcess,
                    kpis.DeductedFromEmployees, kpis.BorneByCompany, kpis.Accounts, waived, kpis.UnassessedExcess);
            }).ToList(),
            BuildBillRanges(bills),
            BuildRepeatOverLimit(windowBatches.ToDictionary(option => option.Id, Period), allBills, selected.Id),
            await BuildAssetsAsync(cancellationToken));
    }

    private static int Period(InsightsBatchOption option) => InsightsRules.Period(option.BillingYear, option.BillingMonth);

    // Excluded bills are left out, as they are in the monthly bill report.
    private async Task<List<BillFacts>> LoadBillsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken)
    {
        if (batchIds.Count == 0) return [];
        return await db.MonthlyBills.AsNoTracking()
            .Where(bill => batchIds.Contains(bill.BillLine.BillBatchId) && bill.Status != MonthlyBillStatus.Excluded)
            .Select(bill => new BillFacts(
                bill.BillLine.BillBatchId, bill.MobileNumberSnapshot, bill.EmployeeEpfSnapshot, bill.EmployeeNameSnapshot, bill.CallingNameSnapshot,
                bill.FactoryCodeSnapshot, bill.FactoryNameSnapshot ?? bill.FactoryCodeSnapshot,
                bill.DepartmentCodeSnapshot, bill.DepartmentNameSnapshot ?? bill.DepartmentCodeSnapshot,
                bill.CategoryCodeSnapshot, bill.CategoryNameSnapshot ?? bill.CategoryCodeSnapshot,
                bill.ActualBill, bill.CreditLimit + bill.MonthlyRental, bill.CalculatedExcess, bill.FinalDeduction, bill.Responsibility, bill.Remark, bill.PackageCodeSnapshot))
            .ToListAsync(cancellationToken);
    }

    private static InsightsKpis BuildKpis(IReadOnlyCollection<BillFacts> bills, decimal roaming) => new(
        bills.Sum(bill => bill.ActualBill),
        bills.Sum(bill => bill.Entitlement),
        bills.Sum(bill => bill.CalculatedExcess),
        bills.Where(bill => bill.Responsibility == Responsibility.ByUser).Sum(bill => bill.FinalDeduction),
        bills.Where(bill => bill.Responsibility == Responsibility.ByCompany).Sum(bill => bill.CalculatedExcess),
        bills.Where(bill => bill.Responsibility is null).Sum(bill => bill.CalculatedExcess),
        roaming,
        bills.Count,
        bills.Count(bill => bill.CalculatedExcess > 0m));

    // The charge types that make up "Charges for Bill Period" on the PDF. Discounts and adjustments are usually credits.
    private static IReadOnlyList<InsightsAmount> BuildChargeMix(IReadOnlyCollection<BillLine> lines) => new[]
    {
        new InsightsAmount("usage", "Usage", lines.Sum(line => line.TotalUsageCharges)),
        new InsightsAmount("idd", "IDD", lines.Sum(line => line.Idd)),
        new InsightsAmount("roaming", "Roaming", lines.Sum(line => line.Roaming)),
        new InsightsAmount("vas", "Value Added Services", lines.Sum(line => line.ValueAddedServices)),
        new InsightsAmount("addToBill", "Add To Bill", lines.Sum(line => line.AddToBill)),
        new InsightsAmount("commitment", "Commitment Charges", lines.Sum(line => line.CommitmentCharges)),
        new InsightsAmount("instalments", "Instalment Plans", lines.Sum(line => line.InstalmentPlans)),
        new InsightsAmount("latePayment", "Late Payment Charges", lines.Sum(line => line.LatePaymentCharges)),
        new InsightsAmount("adjustments", "Bill Adjustments", lines.Sum(line => line.BillAdjustmentsBalanceTransfers)),
        new InsightsAmount("discounts", "Discounts", lines.Sum(line => line.Discounts)),
        new InsightsAmount("taxes", "Government Taxes & Levies", lines.Sum(line => line.GovernmentTaxesAndLevies)),
        new InsightsAmount("vat", "VAT", lines.Sum(line => line.Vat)),
    }.Where(item => item.Amount != 0m).ToList();

    // Who ends up paying each rupee of calculated excess. The part of a By User excess waived by a deduction
    // override is paid by the company, so it counts as By Company.
    private static IReadOnlyList<InsightsAmount> BuildExcessSplit(IReadOnlyCollection<BillFacts> bills)
    {
        var byUser = bills.Where(bill => bill.Responsibility == Responsibility.ByUser).ToList();
        var byCompany = bills.Where(bill => bill.Responsibility == Responsibility.ByCompany).ToList();
        return new[]
        {
            new InsightsAmount("deducted", "Deducted from employees", byUser.Sum(bill => bill.FinalDeduction)),
            new InsightsAmount("company", "By Company", byCompany.Sum(bill => bill.CalculatedExcess) + byUser.Sum(bill => bill.CalculatedExcess - bill.FinalDeduction)),
            new InsightsAmount("unassessed", "Not yet assigned", bills.Where(bill => bill.Responsibility is null).Sum(bill => bill.CalculatedExcess)),
        }.Where(item => item.Amount != 0m).ToList();
    }

    private static IReadOnlyList<InsightsGroupRow> Group(IEnumerable<BillFacts> bills, Func<BillFacts, string> code, Func<BillFacts, string> name) => bills
        .GroupBy(code)
        .Select(group => new InsightsGroupRow(group.Key, name(group.First()), group.Count(), group.Sum(bill => bill.ActualBill), group.Sum(bill => bill.Entitlement), group.Sum(bill => bill.CalculatedExcess)))
        .OrderByDescending(row => row.ActualBill)
        .ToList();

    private static IReadOnlyList<InsightsBand> BuildBillRanges(IReadOnlyCollection<BillFacts> bills)
    {
        var byBand = bills.ToLookup(bill => InsightsRules.ExcessBand(bill.CalculatedExcess));
        return InsightsRules.ExcessBands
            .Select((band, index) => new InsightsBand(band.Key, band.Label, byBand[index].Count(), byBand[index].Sum(bill => bill.CalculatedExcess)))
            .ToList();
    }

    // Counts billing months, not batches, so two batches in one month count once.
    private static InsightsRepeatOverLimit BuildRepeatOverLimit(IReadOnlyDictionary<Guid, int> windowPeriods, ILookup<Guid, BillFacts> allBills, Guid selectedId)
    {
        var repeats = windowPeriods
            .SelectMany(batch => allBills[batch.Key].Select(bill => (Period: batch.Value, Bill: bill)))
            .GroupBy(item => item.Bill.MobileNumber)
            .Select(group =>
            {
                var latest = group.OrderByDescending(item => item.Period).First().Bill;
                var over = group.Where(item => item.Bill.CalculatedExcess > 0m).ToList();
                var monthsOver = over.Select(item => item.Period).Distinct().Count();
                var totalExcess = over.Sum(item => item.Bill.CalculatedExcess);
                return new InsightsRepeatAccount(group.Key, latest.Epf, latest.EmployeeName, latest.FactoryName, latest.PackageCode,
                    monthsOver, group.Select(item => item.Period).Distinct().Count(), totalExcess,
                    monthsOver == 0 ? 0m : decimal.Round(totalExcess / monthsOver, 2, MidpointRounding.AwayFromZero),
                    allBills[selectedId].Where(bill => bill.MobileNumber == group.Key).Sum(bill => bill.CalculatedExcess));
            })
            .Where(account => InsightsRules.IsRepeatOverLimit(account.MonthsOverLimit))
            .OrderByDescending(account => account.MonthsOverLimit).ThenByDescending(account => account.TotalExcess).ThenBy(account => account.MobileNumber, StringComparer.Ordinal)
            .ToList();
        return new InsightsRepeatOverLimit(InsightsRules.RepeatWindowMonths, InsightsRules.RepeatOverLimitMonths, repeats.Count, repeats.Take(RepeatAccountsShown).ToList());
    }

    private static readonly Dictionary<SimType, string> SimTypeLabels = new() { [SimType.Voice] = "Voice", [SimType.VoiceData] = "Voice + Data", [SimType.Data] = "Data", [SimType.ESim] = "eSIM" };

    private static readonly Dictionary<DeviceStatus, string> DeviceStatusLabels = new()
    {
        [DeviceStatus.InStock] = "In Stock", [DeviceStatus.Issued] = "Issued", [DeviceStatus.ReturnPending] = "Return Pending", [DeviceStatus.UnderRepair] = "Under Repair",
        [DeviceStatus.Damaged] = "Damaged", [DeviceStatus.Lost] = "Lost", [DeviceStatus.Retired] = "Retired",
    };

    // Active allocations (pooled SIMs included) by SIM type, and devices by status with the depreciated value still with employees.
    private async Task<InsightsAssets> BuildAssetsAsync(CancellationToken cancellationToken)
    {
        var simTypes = await db.MobileAccounts.AsNoTracking().Where(account => account.IsActive)
            .GroupBy(account => account.SimType).Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var devices = await db.MobileDevices.AsNoTracking()
            .Select(device => new { device.Status, device.PurchaseCost, device.PurchaseDate })
            .ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        return new InsightsAssets(
            Enum.GetValues<SimType>().Select(type => (SimType?)type).Append(null)
                .Select(type => new InsightsCount(type?.ToString() ?? "NotSet", type is { } known ? SimTypeLabels[known] : "Not set", simTypes.Where(item => item.Key == type).Sum(item => item.Count)))
                .Where(item => item.Count > 0).ToList(),
            Enum.GetValues<DeviceStatus>()
                .Select(status => new InsightsCount(status.ToString(), DeviceStatusLabels[status], devices.Count(device => device.Status == status)))
                .Where(item => item.Count > 0).ToList(),
            devices.Count,
            devices.Where(device => device.Status is DeviceStatus.Issued or DeviceStatus.ReturnPending)
                .Sum(device => DeviceRules.RecoverableAmount(device.PurchaseCost, device.PurchaseDate, today)));
    }

    private sealed record BillFacts(
        Guid BatchId, string MobileNumber, string Epf, string EmployeeName, string? CallingName,
        string FactoryCode, string FactoryName, string DepartmentCode, string DepartmentName, string CategoryCode, string CategoryName,
        decimal ActualBill, decimal Entitlement, decimal CalculatedExcess, decimal FinalDeduction, Responsibility? Responsibility, string? Remark, string? PackageCode);
}
