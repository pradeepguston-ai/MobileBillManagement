using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Insights;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Billing;

public sealed class BillingInsightsServiceTests
{
    [Fact]
    public async Task Kpis_split_the_excess_by_who_pays_and_leave_out_excluded_bills()
    {
        await using var db = CreateDb();
        var batch = Batch(db, 2026, 8, BillBatchStatus.Completed);
        // entitlement 125 on every bill
        Bill(db, batch, "711", actual: 165m, excess: 40m, Responsibility.ByUser, deduction: 40m);
        Bill(db, batch, "712", actual: 145m, excess: 20m, Responsibility.ByUser, deduction: 5m, remark: "Half approved");
        Bill(db, batch, "713", actual: 175m, excess: 50m, Responsibility.ByCompany, deduction: 0m, remark: "Roaming", roaming: 30m);
        Bill(db, batch, "714", actual: 135m, excess: 10m, Responsibility.ByCompany, deduction: 0m);
        Bill(db, batch, "715", actual: 133m, excess: 8m, responsibility: null, deduction: 0m);
        Bill(db, batch, "716", actual: 100m, excess: 0m, Responsibility.ByUser, deduction: 0m);
        Bill(db, batch, "717", actual: 999m, excess: 874m, Responsibility.ByUser, deduction: 874m, status: MonthlyBillStatus.Excluded);
        await db.SaveChangesAsync();

        var result = await new EfBillingInsightsService(db).GetAsync(null, 6, default);

        var kpis = result.Current!;
        Assert.Equal(853m, kpis.TotalActualBill);
        Assert.Equal(750m, kpis.TotalEntitlement);
        Assert.Equal(128m, kpis.TotalCalculatedExcess);
        Assert.Equal(45m, kpis.DeductedFromEmployees);
        Assert.Equal(60m, kpis.BorneByCompany);
        Assert.Equal(8m, kpis.UnassessedExcess);
        Assert.Equal(6, kpis.Accounts);
        Assert.Equal(5, kpis.OverLimitAccounts);

        var split = result.ExcessSplit.ToDictionary(item => item.Key, item => item.Amount);
        Assert.Equal(45m, split["deducted"]);
        Assert.Equal(15m, split["waived"]);
        Assert.Equal(50m, split["companyRoaming"]);
        Assert.Equal(10m, split["company"]);
        Assert.Equal(8m, split["unassessed"]);
        Assert.Equal(kpis.TotalCalculatedExcess, split.Values.Sum());
    }

    [Fact]
    public async Task Charge_mix_groups_and_top_accounts_come_from_the_selected_batch()
    {
        await using var db = CreateDb();
        var batch = Batch(db, 2026, 8, BillBatchStatus.Completed);
        Bill(db, batch, "711", actual: 165m, excess: 40m, Responsibility.ByUser, deduction: 40m, factory: ("F1", "Factory One"), roaming: 12m, usage: 100m, vat: 20m);
        Bill(db, batch, "712", actual: 300m, excess: 175m, Responsibility.ByUser, deduction: 175m, factory: ("F2", "Factory Two"), usage: 250m, vat: 50m);
        Bill(db, batch, "713", actual: 100m, excess: 0m, Responsibility.ByUser, deduction: 0m, factory: ("F1", "Factory One"), usage: 80m, discounts: -5m);
        await db.SaveChangesAsync();

        var result = await new EfBillingInsightsService(db).GetAsync(batch.Id, 6, default);

        var mix = result.ChargeMix.ToDictionary(item => item.Key, item => item.Amount);
        Assert.Equal(430m, mix["usage"]);
        Assert.Equal(12m, mix["roaming"]);
        Assert.Equal(70m, mix["vat"]);
        Assert.Equal(-5m, mix["discounts"]);
        Assert.False(mix.ContainsKey("idd"));   // zero amounts are left out

        Assert.Equal(["F2", "F1"], result.Groups.Factory.Select(row => row.Code));
        var factoryOne = result.Groups.Factory.Single(row => row.Code == "F1");
        Assert.Equal(2, factoryOne.Accounts);
        Assert.Equal(265m, factoryOne.ActualBill);
        Assert.Equal(250m, factoryOne.Entitlement);
        Assert.Equal(40m, factoryOne.CalculatedExcess);

        Assert.Equal(["712", "711"], result.TopOverLimit.Select(item => item.MobileNumber));
        Assert.Equal("Factory Two", result.TopOverLimit[0].Factory);
    }

    [Fact]
    public async Task Comparison_uses_the_latest_earlier_approved_batch_and_the_trend_marks_a_provisional_batch()
    {
        await using var db = CreateDb();
        var may = Batch(db, 2026, 5, BillBatchStatus.Locked);
        var june = Batch(db, 2026, 6, BillBatchStatus.Completed);
        var july = Batch(db, 2026, 7, BillBatchStatus.HRApproval);   // not approved: provisional
        var draft = Batch(db, 2026, 9, BillBatchStatus.Parsed);      // never matched: not offered
        Bill(db, may, "711", actual: 100m, excess: 0m, Responsibility.ByUser, deduction: 0m);
        Bill(db, june, "711", actual: 200m, excess: 75m, Responsibility.ByUser, deduction: 75m);
        Bill(db, july, "711", actual: 300m, excess: 175m, Responsibility.ByCompany, deduction: 0m);
        await db.SaveChangesAsync();

        var result = await new EfBillingInsightsService(db).GetAsync(null, 6, default);

        Assert.Equal([july.Id, june.Id, may.Id], result.Batches.Select(option => option.Id));
        Assert.DoesNotContain(result.Batches, option => option.Id == draft.Id);
        Assert.Equal(july.Id, result.Selected!.Id);
        Assert.False(result.Selected.IsApproved);
        Assert.Equal(june.Id, result.PreviousBatch!.Id);
        Assert.Equal(200m, result.Previous!.TotalActualBill);
        Assert.Equal(75m, result.Previous.DeductedFromEmployees);

        Assert.Equal([may.Id, june.Id, july.Id], result.Trend.Select(point => point.BatchId));
        Assert.Equal([true, true, false], result.Trend.Select(point => point.IsApproved));
        Assert.Equal(175m, result.Trend[2].BorneByCompany);

        var limited = await new EfBillingInsightsService(db).GetAsync(june.Id, 1, default);
        Assert.Equal([june.Id], limited.Trend.Select(point => point.BatchId));
        Assert.Equal(may.Id, limited.PreviousBatch!.Id);
    }

    [Fact]
    public async Task No_matched_batches_gives_an_empty_result_and_an_unknown_batch_is_not_found()
    {
        await using var db = CreateDb();

        var empty = await new EfBillingInsightsService(db).GetAsync(null, 6, default);

        Assert.Empty(empty.Batches);
        Assert.Null(empty.Selected);
        Assert.Null(empty.Current);
        await Assert.ThrowsAsync<BillBatchNotFoundException>(() => new EfBillingInsightsService(db).GetAsync(Guid.NewGuid(), 6, default));
    }

    private static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static BillBatch Batch(MobileBillDbContext db, int year, int month, BillBatchStatus status)
    {
        var provider = new TelecomProvider { Code = $"TEL-{Guid.NewGuid():N}"[..12], Name = "Dialog" };
        var batch = new BillBatch { ProviderId = provider.Id, Provider = provider, CorporateCode = "CORP", BillingYear = year, BillingMonth = month, Status = status };
        db.AddRange(provider, batch);
        return batch;
    }

    private static void Bill(MobileBillDbContext db, BillBatch batch, string mobile, decimal actual, decimal excess, Responsibility? responsibility, decimal deduction,
        string? remark = null, decimal roaming = 0m, decimal usage = 0m, decimal vat = 0m, decimal discounts = 0m,
        (string Code, string Name)? factory = null, MonthlyBillStatus status = MonthlyBillStatus.Approved)
    {
        var (factoryCode, factoryName) = factory ?? ("FAC", "Factory");
        var line = new BillLine
        {
            BillBatchId = batch.Id, BillBatch = batch, MobileNumber = mobile, PageNumber = 1, RawText = "row", ExtractionStatus = BillLineExtractionStatus.Extracted,
            TotalDueAmount = actual, Roaming = roaming, TotalUsageCharges = usage, Vat = vat, Discounts = discounts,
        };
        db.AddRange(line, new MonthlyBill
        {
            EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = line.Id, BillLine = line,
            EmployeeEpfSnapshot = "EPF", EmployeeNameSnapshot = $"Employee {mobile}", MobileNumberSnapshot = mobile,
            CategoryCodeSnapshot = "CAT", CategoryNameSnapshot = "Category", DesignationCodeSnapshot = "DES", DesignationNameSnapshot = "Designation",
            FactoryCodeSnapshot = factoryCode, FactoryNameSnapshot = factoryName, DepartmentCodeSnapshot = "DEP", DepartmentNameSnapshot = "Department",
            EntitlementEffectiveFromSnapshot = new DateOnly(2026, 1, 1),
            CreditLimit = 100m, MonthlyRental = 25m, ActualBill = actual, Variance = 125m - actual, CalculatedExcess = excess,
            Responsibility = responsibility, FinalDeduction = deduction, AssessedAt = responsibility is null ? null : DateTimeOffset.UnixEpoch,
            Remark = remark, Status = status,
        });
    }
}
