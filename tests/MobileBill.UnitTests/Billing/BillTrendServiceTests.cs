using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Billing;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Billing;

public sealed class BillTrendServiceTests
{
    private static readonly Guid Kasun = Guid.NewGuid();
    private static readonly Guid Nimal = Guid.NewGuid();

    [Fact]
    public async Task This_number_shows_only_the_months_this_employee_held_it_up_to_the_reviewed_batch()
    {
        var (db, seed) = await SeedAsync();
        await using var _ = db;

        var trend = await new EfBillTrendService(db).GetAsync(seed.SeptemberBatch, seed.SeptemberBill, new BillTrendRequest(), default);

        // June belonged to Nimal; October is after the reviewed batch.
        Assert.Equal([7, 8, 9], trend.Points.Select(point => point.BillingMonth));
        var september = trend.Points[^1];
        Assert.Equal((2600m, 1500m, 1100m, 1100m, 200m, "ByUser", true, true, true), (september.ActualBill, september.Entitlement, september.CalculatedExcess, september.FinalDeduction, september.Vas, september.Responsibility, september.IsOverLimit, september.IsPreliminary, september.IsCurrent));
        Assert.False(trend.Points[0].IsPreliminary);                                     // July is Locked
        Assert.Equal((1500m, 73.3m, true), (trend.Average!.Value, trend.ChangePercent!.Value, trend.IsAboveUsual));   // (1000 + 2000) / 2
        Assert.Equal((2, 2026, 9, 2600m), (trend.MonthsOverLimit, trend.HighestYear!.Value, trend.HighestMonth!.Value, trend.HighestActualBill!.Value));
        Assert.Equal((12, "EPF-1", "0771"), (trend.Months, trend.EmployeeEpf, trend.MobileNumber));
    }

    [Fact]
    public async Task Employee_scope_adds_up_all_of_the_employees_numbers_per_month()
    {
        var (db, seed) = await SeedAsync();
        await using var _ = db;

        var trend = await new EfBillTrendService(db).GetAsync(seed.SeptemberBatch, seed.SeptemberBill, new BillTrendRequest(BillTrendScope.Employee), default);

        var september = trend.Points.Single(point => point.IsCurrent);
        Assert.Equal((2, 3000m, 2900m, "Mixed"), (september.Numbers, september.ActualBill, september.Entitlement, september.Responsibility));
        Assert.Equal(3000m, trend.CurrentActualBill);
    }

    [Fact]
    public async Task All_holders_scope_includes_earlier_holders_and_marks_them()
    {
        var (db, seed) = await SeedAsync();
        await using var _ = db;

        var trend = await new EfBillTrendService(db).GetAsync(seed.SeptemberBatch, seed.SeptemberBill, new BillTrendRequest(BillTrendScope.AllHolders, 6), default);

        Assert.Equal([6, 7, 8, 9], trend.Points.Select(point => point.BillingMonth));
        Assert.Equal(("EPF-2", "Nimal", true), (trend.Points[0].HolderEpf, trend.Points[0].HolderName, trend.Points[0].IsOtherHolder));
        Assert.False(trend.Points[1].IsOtherHolder);
        Assert.Equal(6, trend.Months);
    }

    [Fact]
    public async Task First_month_has_no_average_and_is_not_above_usual()
    {
        var (db, seed) = await SeedAsync();
        await using var _ = db;

        var trend = await new EfBillTrendService(db).GetAsync(seed.JulyBatch, seed.JulyBill, new BillTrendRequest(), default);

        Assert.Single(trend.Points);
        Assert.Equal((null, null, false), (trend.Average, trend.ChangePercent, trend.IsAboveUsual));
    }

    [Fact]
    public async Task A_bill_from_another_batch_is_not_found()
    {
        var (db, seed) = await SeedAsync();
        await using var _ = db;

        await Assert.ThrowsAsync<BillReviewRowNotFoundException>(() => new EfBillTrendService(db).GetAsync(seed.JulyBatch, seed.SeptemberBill, new BillTrendRequest(), default));
    }

    private sealed record Seed(Guid JulyBatch, Guid JulyBill, Guid SeptemberBatch, Guid SeptemberBill);

    // 0771: Nimal in June, then Kasun from July. Kasun also has 0779 in September. October is a later batch.
    private static async Task<(MobileBillDbContext Db, Seed Seed)> SeedAsync()
    {
        var db = new MobileBillDbContext(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var provider = new TelecomProvider { Code = "DIALOG", Name = "Dialog" };
        db.Add(provider);
        var june = Batch(provider, 6, BillBatchStatus.Locked);
        var july = Batch(provider, 7, BillBatchStatus.Locked);
        var august = Batch(provider, 8, BillBatchStatus.Completed);
        var september = Batch(provider, 9, BillBatchStatus.HRApproval);
        var october = Batch(provider, 10, BillBatchStatus.Validated);
        db.AddRange(june, july, august, september, october);
        Bill(db, june, Nimal, "EPF-2", "Nimal", "0771", 900m, 0m, Responsibility.ByUser);
        var julyBill = Bill(db, july, Kasun, "EPF-1", "Kasun", "0771", 1000m, 0m, Responsibility.ByUser);
        Bill(db, august, Kasun, "EPF-1", "Kasun", "0771", 2000m, 50m, Responsibility.ByCompany);
        var septemberBill = Bill(db, september, Kasun, "EPF-1", "Kasun", "0771", 2600m, 200m, Responsibility.ByUser);
        Bill(db, september, Kasun, "EPF-1", "Kasun", "0779", 400m, 0m, Responsibility.ByCompany, creditLimit: 900m);
        Bill(db, october, Kasun, "EPF-1", "Kasun", "0771", 9000m, 0m, null);
        await db.SaveChangesAsync();
        return (db, new Seed(july.Id, julyBill.Id, september.Id, septemberBill.Id));
    }

    private static BillBatch Batch(TelecomProvider provider, int month, BillBatchStatus status) =>
        new() { ProviderId = provider.Id, Provider = provider, CorporateCode = "PR1", BillingYear = 2026, BillingMonth = month, Status = status };

    private static MonthlyBill Bill(MobileBillDbContext db, BillBatch batch, Guid employeeId, string epf, string name, string mobile, decimal actual, decimal vas, Responsibility? responsibility, decimal creditLimit = 1000m)
    {
        var line = new BillLine { BillBatchId = batch.Id, MobileNumber = mobile, PageNumber = 1, RawText = mobile, ExtractionStatus = BillLineExtractionStatus.Extracted, ValueAddedServices = vas, TotalDueAmount = actual };
        db.Add(line);
        var excess = Math.Max(0m, actual - creditLimit - 500m);
        var bill = new MonthlyBill
        {
            EmployeeId = employeeId, MobileAccountId = Guid.NewGuid(), BillLineId = line.Id, CreditLimit = creditLimit, MonthlyRental = 500m, ActualBill = actual,
            CalculatedExcess = excess, FinalDeduction = responsibility == Responsibility.ByUser ? excess : 0m, Responsibility = responsibility,
            EmployeeEpfSnapshot = epf, EmployeeNameSnapshot = name, CategoryCodeSnapshot = "C", DesignationCodeSnapshot = "D",
            FactoryCodeSnapshot = "FA", DepartmentCodeSnapshot = "DEP", MobileNumberSnapshot = mobile
        };
        db.Add(bill);
        return bill;
    }
}
