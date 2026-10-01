using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Billing;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Billing;

public sealed class BillAssessmentServiceTests
{
    [Fact]
    public async Task ByCompany_sets_zero_deduction_and_server_assessment_metadata()
    {
        await using var db = CreateDb(); var bill = await SeedAsync(db, 150m, 100m, 10m);
        var clock = new TestClock();

        await new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), clock)
            .AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByCompany, null, null), default);

        var saved = await db.MonthlyBills.SingleAsync();
        Assert.Equal(Responsibility.ByCompany, saved.Responsibility);
        Assert.Equal(0m, saved.FinalDeduction);
        Assert.Equal("reviewer", saved.AssessedBy);
        Assert.Equal(clock.UtcNow, saved.AssessedAt);
        Assert.Null(saved.DeductionOverrideAmount);
    }

    [Fact]
    public async Task ByUser_defaults_to_calculated_excess_without_override_reason()
    {
        await using var db = CreateDb(); var bill = await SeedAsync(db, 150m, 100m, 10m);

        await new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock())
            .AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByUser, null, null), default);

        var saved = await db.MonthlyBills.SingleAsync();
        Assert.Equal(40m, saved.FinalDeduction);
        Assert.Null(saved.DeductionOverrideReason);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(0)]
    public async Task ByUser_allows_lower_or_zero_override_when_reason_is_supplied(decimal amount)
    {
        await using var db = CreateDb(); var bill = await SeedAsync(db, 150m, 100m, 10m);
        var service = new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock());

        await service.AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByUser, amount, "Approved waiver"), default);

        var saved = await db.MonthlyBills.SingleAsync();
        Assert.Equal(amount, saved.FinalDeduction);
        Assert.Equal(amount, saved.DeductionOverrideAmount);
        Assert.Equal("Approved waiver", saved.DeductionOverrideReason);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(40.01)]
    public async Task ByUser_rejects_deduction_outside_zero_to_excess_range(decimal amount)
    {
        await using var db = CreateDb(); var bill = await SeedAsync(db, 150m, 100m, 10m);
        var service = new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock());

        await Assert.ThrowsAsync<BillAssessmentValidationException>(() => service.AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByUser, amount, "reason"), default));
        Assert.Null((await db.MonthlyBills.SingleAsync()).AssessedAt);
    }

    [Fact]
    public async Task Changed_by_user_deduction_requires_reason_but_zero_excess_default_does_not()
    {
        await using var db = CreateDb(); var bill = await SeedAsync(db, 110m, 100m, 10m);
        var service = new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock());
        await service.AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByUser, null, null), default);
        Assert.Equal(0m, (await db.MonthlyBills.SingleAsync()).FinalDeduction);

        await using var db2 = CreateDb(); var bill2 = await SeedAsync(db2, 150m, 100m, 10m);
        var service2 = new EfBillAssessmentService(db2, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock());
        await Assert.ThrowsAsync<BillAssessmentValidationException>(() => service2.AssessAsync(bill2.Id, new AssessMonthlyBillRequest(Responsibility.ByUser, 0m, null), default));
    }

    [Fact]
    public async Task Reassessment_requires_reason_and_audits_previous_and_new_values()
    {
        await using var db = CreateDb(); var bill = await SeedAsync(db, 150m, 100m, 10m);
        var service = new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock());
        await service.AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByUser, null, null), default);

        await Assert.ThrowsAsync<BillAssessmentValidationException>(() => service.AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByCompany, null, null), default));
        await service.AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByCompany, null, "Company accepted charge"), default);

        var audits = await db.AuditLogs.ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Contains("ByUser", audits.Last().BeforeDataJson);
        Assert.Contains("ByCompany", audits.Last().AfterDataJson);
    }

    [Fact]
    public async Task Unauthorized_assessment_changes_nothing()
    {
        await using var db = CreateDb(); var bill = await SeedAsync(db, 150m, 100m, 10m);
        var service = new EfBillAssessmentService(db, new FixedAuthorization(false), new TestUser("other"), new TestClock());

        await Assert.ThrowsAsync<BillReviewForbiddenException>(() => service.AssessAsync(bill.Id, new AssessMonthlyBillRequest(Responsibility.ByUser, null, null), default));
        Assert.Null((await db.MonthlyBills.SingleAsync()).AssessedAt);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Bulk_assessment_assigns_responsibility_to_every_selected_bill_with_its_own_default_deduction()
    {
        await using var db = CreateDb();
        var billA = await SeedAsync(db, 150m, 100m, 10m);
        var billB = await SeedAsync(db, 200m, 100m, 10m);
        var service = new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock());

        var result = await service.BulkAssessAsync(new BulkAssessMonthlyBillsRequest([billA.Id, billB.Id], Responsibility.ByUser, null), default);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(40m, (await db.MonthlyBills.SingleAsync(x => x.Id == billA.Id)).FinalDeduction);
        Assert.Equal(90m, (await db.MonthlyBills.SingleAsync(x => x.Id == billB.Id)).FinalDeduction);
    }

    [Fact]
    public async Task Bulk_assessment_reports_a_per_item_failure_without_blocking_the_rest()
    {
        await using var db = CreateDb();
        var lockedBatch = new BillBatch { ProviderId = Guid.NewGuid(), CorporateCode = "C", BillingYear = 2026, BillingMonth = 9, Status = BillBatchStatus.Locked };
        var lockedLine = new BillLine { BillBatchId = lockedBatch.Id, MobileNumber = "761499199", PageNumber = 1, RawText = "row", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 150m };
        var lockedBill = new MonthlyBill { EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = lockedLine.Id, BillLine = lockedLine, CreditLimit = 100m, MonthlyRental = 10m, ActualBill = 150m, EmployeeEpfSnapshot = "EPF", EmployeeNameSnapshot = "Employee", MobileNumberSnapshot = lockedLine.MobileNumber, CategoryCodeSnapshot = "CAT", DesignationCodeSnapshot = "DES", FactoryCodeSnapshot = "FAC", DepartmentCodeSnapshot = "DEP", EntitlementEffectiveFromSnapshot = new DateOnly(2026, 9, 1) };
        db.AddRange(lockedBatch, lockedLine, lockedBill); await db.SaveChangesAsync();
        var openBill = await SeedAsync(db, 150m, 100m, 10m);
        var service = new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock());

        var result = await service.BulkAssessAsync(new BulkAssessMonthlyBillsRequest([lockedBill.Id, openBill.Id], Responsibility.ByCompany, null), default);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        var failed = Assert.Single(result.Items, item => !item.Success);
        Assert.Equal(lockedBill.Id, failed.MonthlyBillId);
        Assert.Contains("Locked", failed.Error);
        Assert.Equal(0m, (await db.MonthlyBills.SingleAsync(x => x.Id == openBill.Id)).FinalDeduction);
        Assert.Null((await db.MonthlyBills.SingleAsync(x => x.Id == lockedBill.Id)).AssessedAt);
    }

    [Fact]
    public async Task Bulk_assessment_rejects_an_empty_selection()
    {
        await using var db = CreateDb();
        var service = new EfBillAssessmentService(db, new FixedAuthorization(true), new TestUser("reviewer"), new TestClock());

        await Assert.ThrowsAsync<BillAssessmentValidationException>(() => service.BulkAssessAsync(new BulkAssessMonthlyBillsRequest([], Responsibility.ByCompany, null), default));
    }

    [Fact]
    public async Task Unauthorized_bulk_assessment_changes_nothing()
    {
        await using var db = CreateDb(); var bill = await SeedAsync(db, 150m, 100m, 10m);
        var service = new EfBillAssessmentService(db, new FixedAuthorization(false), new TestUser("other"), new TestClock());

        await Assert.ThrowsAsync<BillReviewForbiddenException>(() => service.BulkAssessAsync(new BulkAssessMonthlyBillsRequest([bill.Id], Responsibility.ByUser, null), default));
        Assert.Null((await db.MonthlyBills.SingleAsync()).AssessedAt);
    }

    private static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<MonthlyBill> SeedAsync(MobileBillDbContext db, decimal totalDue, decimal credit, decimal rental)
    {
        var line = new BillLine { MobileNumber = "761499198", PageNumber = 1, RawText = "row", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = totalDue };
        var bill = new MonthlyBill { EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = line.Id, BillLine = line, CreditLimit = credit, MonthlyRental = rental, ActualBill = totalDue, EmployeeEpfSnapshot = "EPF", EmployeeNameSnapshot = "Employee", MobileNumberSnapshot = line.MobileNumber, CategoryCodeSnapshot = "CAT", DesignationCodeSnapshot = "DES", FactoryCodeSnapshot = "FAC", DepartmentCodeSnapshot = "DEP", EntitlementEffectiveFromSnapshot = new DateOnly(2026, 9, 1) };
        db.AddRange(line, bill); await db.SaveChangesAsync(); return bill;
    }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero); }
    private sealed class TestUser(string id) : ICurrentUserService { public string UserId => id; public string DisplayName => id; public UserRole Role => UserRole.ITEngineer; }
    private sealed class FixedAuthorization(bool value) : IBillReviewAuthorizationService { public Task<bool> CanResolveExceptionsAsync(CancellationToken cancellationToken) => Task.FromResult(value); }
}
