using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
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

        var result = await Service(db).GetAsync(null, 6, default);

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
        Assert.Equal(["deducted", "company", "unassessed"], result.ExcessSplit.Select(item => item.Key));
        Assert.Equal(45m, split["deducted"]);
        Assert.Equal(75m, split["company"]);                                   // 50 roaming + 10 other + 15 waived by override
        Assert.Equal(8m, split["unassessed"]);
        Assert.Equal(["Deducted from employees", "By Company", "Not yet assigned"], result.ExcessSplit.Select(item => item.Label));
        Assert.Equal(kpis.TotalCalculatedExcess, split.Values.Sum());

        var point = Assert.Single(result.Trend);
        Assert.Equal((45m, 15m, 60m, 8m), (point.DeductedFromEmployees, point.WaivedForEmployees, point.BorneByCompany, point.UnassessedExcess));
        Assert.Equal(point.TotalCalculatedExcess, point.DeductedFromEmployees + point.WaivedForEmployees + point.BorneByCompany + point.UnassessedExcess);
    }

    [Fact]
    public async Task Bill_ranges_count_the_numbers_in_each_excess_band()
    {
        await using var db = CreateDb();
        var batch = Batch(db, 2026, 8, BillBatchStatus.Completed);
        Bill(db, batch, "711", actual: 100m, excess: 0m, Responsibility.ByUser, deduction: 0m);
        Bill(db, batch, "712", actual: 425m, excess: 300m, Responsibility.ByUser, deduction: 300m);
        Bill(db, batch, "713", actual: 625m, excess: 500m, Responsibility.ByUser, deduction: 500m);
        Bill(db, batch, "714", actual: 2625m, excess: 2500m, Responsibility.ByCompany, deduction: 0m);
        Bill(db, batch, "715", actual: 6125m, excess: 6000m, Responsibility.ByCompany, deduction: 0m);
        Bill(db, batch, "716", actual: 9999m, excess: 9874m, Responsibility.ByUser, deduction: 9874m, status: MonthlyBillStatus.Excluded);
        await db.SaveChangesAsync();

        var result = await Service(db).GetAsync(batch.Id, 6, default);

        Assert.Equal(["Within limit", "Up to 500", "500 – 2,000", "2,000 – 5,000", "Over 5,000"], result.BillRanges.Select(band => band.Label));
        Assert.Equal([1, 2, 0, 1, 1], result.BillRanges.Select(band => band.Accounts));
        Assert.Equal(800m, result.BillRanges[1].CalculatedExcess);
    }

    [Fact]
    public async Task Repeat_over_limit_counts_billing_months_over_the_limit_in_the_last_six_months()
    {
        await using var db = CreateDb();
        var february = Batch(db, 2026, 2, BillBatchStatus.Locked);     // outside the six-month window
        var march = Batch(db, 2026, 3, BillBatchStatus.Locked);
        var may = Batch(db, 2026, 5, BillBatchStatus.Completed);
        var maySecond = Batch(db, 2026, 5, BillBatchStatus.Completed); // a second batch in May counts as the same month
        var june = Batch(db, 2026, 6, BillBatchStatus.HRApproval);     // not approved and not selected: left out
        var august = Batch(db, 2026, 8, BillBatchStatus.ITReview);      // selected, so it counts though not approved
        foreach (var batch in new[] { february, march, may, june, august }) Bill(db, batch, "711", actual: 300m, excess: 175m, Responsibility.ByUser, deduction: 175m, package: "PPU23_700");
        Bill(db, maySecond, "711", actual: 200m, excess: 75m, Responsibility.ByUser, deduction: 75m);
        foreach (var batch in new[] { february, march, june }) Bill(db, batch, "712", actual: 300m, excess: 175m, Responsibility.ByUser, deduction: 175m);
        Bill(db, august, "712", actual: 100m, excess: 0m, Responsibility.ByUser, deduction: 0m);
        foreach (var batch in new[] { march, may, august }) Bill(db, batch, "713", actual: 225m, excess: 100m, Responsibility.ByCompany, deduction: 0m);
        await db.SaveChangesAsync();

        var result = await Service(db).GetAsync(august.Id, 6, default);

        Assert.Equal((6, 3, 2), (result.RepeatOverLimit.WindowMonths, result.RepeatOverLimit.MinMonthsOver, result.RepeatOverLimit.TotalCount));
        Assert.Equal(["711", "713"], result.RepeatOverLimit.Accounts.Select(account => account.MobileNumber));   // 712 was over only in March in the window
        var first = result.RepeatOverLimit.Accounts[0];
        Assert.Equal((3, 3, 600m, 200m, 175m, "PPU23_700"), (first.MonthsOverLimit, first.MonthsBilled, first.TotalExcess, first.AverageExcess, first.SelectedBatchExcess, first.PackageCode));
    }

    [Fact]
    public async Task Assets_count_active_allocations_by_sim_type_and_devices_by_status_with_the_value_still_with_employees()
    {
        await using var db = CreateDb();
        var employee = new Employee { EPF = "EPF-1", FullName = "Holder", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        db.AddRange(employee,
            new MobileAccount { MobileNumber = "0771", EmployeeId = employee.Id, SimType = SimType.VoiceData },
            new MobileAccount { MobileNumber = "0772", EmployeeId = employee.Id, SimType = SimType.VoiceData, Status = SimStatus.Pooled },
            new MobileAccount { MobileNumber = "0773", EmployeeId = employee.Id, SimType = SimType.ESim },
            new MobileAccount { MobileNumber = "0774", EmployeeId = employee.Id },
            new MobileAccount { MobileNumber = "0775", EmployeeId = employee.Id, SimType = SimType.Data, IsActive = false, Status = SimStatus.Disconnected },
            Device("A1", DeviceStatus.Issued, 96000m, new DateOnly(2025, 10, 7)),          // 12 of 48 months used: 72,000 left
            Device("A2", DeviceStatus.ReturnPending, 48000m, new DateOnly(2026, 10, 7)),   // new: 48,000
            Device("A3", DeviceStatus.InStock, 50000m, new DateOnly(2026, 1, 1)),
            Device("A4", DeviceStatus.Lost, 50000m, new DateOnly(2026, 1, 1)));
        await db.SaveChangesAsync();

        var assets = (await Service(db).GetAsync(null, 6, default)).Assets;   // no batches yet: assets still shown

        Assert.Equal([("VoiceData", "Voice + Data", 2), ("ESim", "eSIM", 1), ("NotSet", "Not set", 1)], assets.SimTypes.Select(item => (item.Key, item.Label, item.Count)));
        Assert.Equal([("In Stock", 1), ("Issued", 1), ("Return Pending", 1), ("Lost", 1)], assets.DeviceStatuses.Select(item => (item.Label, item.Count)));
        Assert.Equal((4, 120000m), (assets.Devices, assets.DevicesWithEmployeesValue));
    }

    private static MobileDevice Device(string assetTag, DeviceStatus status, decimal cost, DateOnly purchased) =>
        new() { AssetTag = assetTag, Imei1 = assetTag, Brand = "Brand", Model = "Model", Status = status, PurchaseCost = cost, PurchaseDate = purchased, StatusSince = purchased };

    [Fact]
    public async Task Charge_mix_groups_and_top_accounts_come_from_the_selected_batch()
    {
        await using var db = CreateDb();
        var batch = Batch(db, 2026, 8, BillBatchStatus.Completed);
        Bill(db, batch, "711", actual: 165m, excess: 40m, Responsibility.ByUser, deduction: 40m, factory: ("F1", "Factory One"), roaming: 12m, usage: 100m, vat: 20m);
        Bill(db, batch, "712", actual: 300m, excess: 175m, Responsibility.ByUser, deduction: 175m, factory: ("F2", "Factory Two"), usage: 250m, vat: 50m);
        Bill(db, batch, "713", actual: 100m, excess: 0m, Responsibility.ByUser, deduction: 0m, factory: ("F1", "Factory One"), usage: 80m, discounts: -5m);
        await db.SaveChangesAsync();

        var result = await Service(db).GetAsync(batch.Id, 6, default);

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

        var result = await Service(db).GetAsync(null, 6, default);

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

        var limited = await Service(db).GetAsync(june.Id, 1, default);
        Assert.Equal([june.Id], limited.Trend.Select(point => point.BatchId));
        Assert.Equal(may.Id, limited.PreviousBatch!.Id);
    }

    [Fact]
    public async Task No_matched_batches_gives_an_empty_result_and_an_unknown_batch_is_not_found()
    {
        await using var db = CreateDb();

        var empty = await Service(db).GetAsync(null, 6, default);

        Assert.Empty(empty.Batches);
        Assert.Null(empty.Selected);
        Assert.Null(empty.Current);
        await Assert.ThrowsAsync<BillBatchNotFoundException>(() => Service(db).GetAsync(Guid.NewGuid(), 6, default));
    }

    private static EfBillingInsightsService Service(MobileBillDbContext db) => new(db, new TestClock());

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero); }

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
        (string Code, string Name)? factory = null, MonthlyBillStatus status = MonthlyBillStatus.Approved, string? package = null)
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
            Remark = remark, Status = status, PackageCodeSnapshot = package,
        });
    }
}
