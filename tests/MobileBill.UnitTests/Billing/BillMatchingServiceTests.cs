using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Billing;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Billing;

public sealed class BillMatchingServiceTests
{
    [Fact]
    public async Task Match_uses_allocation_limits_and_preserves_snapshots()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db);

        var result = await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.Equal(1, result.MonthlyBillsCreated);
        Assert.Equal(AllocationMatchMethod.Automatic, monthly.AllocationMatchMethod);
        Assert.Equal(EntitlementMatchMethod.Automatic, monthly.EntitlementMatchMethod);
        Assert.Equal("EPF-1", monthly.EmployeeEpfSnapshot);
        Assert.Equal("Employee One", monthly.EmployeeNameSnapshot);
        Assert.Equal("768791861", monthly.MobileNumberSnapshot);
        Assert.Equal(100m, monthly.CreditLimit);
        Assert.Equal(10m, monthly.MonthlyRental);
        Assert.Equal(150m, monthly.ActualBill);
        Assert.Equal(-40m, monthly.Variance);
    }

    [Theory]
    [InlineData("2026-09-05")]
    [InlineData("2026-08-20")]
    public async Task Pooled_sim_whose_holder_resigned_before_the_10th_is_a_company_bill(string resignedOn)
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db);   // batch is September 2026
        Pool(setup.Account, DateOnly.Parse(resignedOn));
        await db.SaveChangesAsync();

        var result = await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.Equal(0, result.ExceptionsCreated);
        Assert.True(monthly.IsPooled);
        Assert.Equal(("POOL", "SIM Pool (Unassigned)"), (monthly.EmployeeEpfSnapshot, monthly.EmployeeNameSnapshot));
        Assert.Equal((Responsibility.ByCompany, 0m), (monthly.Responsibility!.Value, monthly.FinalDeduction));
        Assert.NotNull(monthly.AssessedAt);
        Assert.Contains("SIM Pool since", monthly.Remark);
        Assert.Contains("EPF-1 – Employee One", monthly.Remark);
        Assert.Equal("F", monthly.FactoryCodeSnapshot);   // still counted under the last holder's factory
        Assert.Equal("AutoAssessedSimPool", (await db.AuditLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task Pooled_sim_whose_holder_resigned_on_or_after_the_10th_is_charged_to_that_employee()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db);
        Pool(setup.Account, new DateOnly(2026, 9, 10));
        await db.SaveChangesAsync();

        await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.False(monthly.IsPooled);
        Assert.Equal(("EPF-1", "Employee One"), (monthly.EmployeeEpfSnapshot, monthly.EmployeeNameSnapshot));
        Assert.Equal((Responsibility.ByUser, 40m), (monthly.Responsibility!.Value, monthly.FinalDeduction));
        Assert.StartsWith("Resigned on 10-Sep-2026", monthly.Remark);
        Assert.Empty(await db.BillExceptions.ToListAsync());
        Assert.Equal("AutoAssessedResignedHolder", (await db.AuditLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task Bill_for_a_disconnected_sim_is_a_company_bill_flagged_for_the_provider()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db, accountIsActive: false);
        setup.Account.Status = SimStatus.Disconnected;
        setup.Account.DisconnectedOn = new DateOnly(2026, 8, 31);
        await db.SaveChangesAsync();

        await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.True(monthly.IsPooled);
        Assert.Equal(Responsibility.ByCompany, monthly.Responsibility);
        Assert.StartsWith("Billed after disconnection on 31-Aug-2026", monthly.Remark);
        var flag = await db.BillExceptions.SingleAsync();
        Assert.Equal((BillExceptionType.BILLED_AFTER_DISCONNECTION, BillExceptionSeverity.Warning, monthly.Id), (flag.ExceptionType, flag.Severity, flag.MonthlyBillId!.Value));
    }

    private static void Pool(MobileAccount account, DateOnly resignedOn)
    {
        account.Status = SimStatus.Pooled;
        account.PooledOn = resignedOn;
        account.StatusReason = "Resigned";
        account.Employee.IsActive = false;
    }

    [Fact]
    public async Task Match_leaves_the_bill_unassessed_when_the_employee_has_no_remembered_responsibility()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db);

        await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.Null(monthly.Responsibility);
        Assert.Null(monthly.AssessedAt);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Match_snapshots_the_employee_section_and_sub_section()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db);
        var section = new Section { Code = "S1", Name = "Infrastructure", DepartmentCode = "D" };
        var subSection = new SubSection { Code = "SS1", Name = "Network", SectionCode = "S1", Section = section };
        db.AddRange(section, subSection);
        setup.Account.Employee.SectionCode = "S1";
        setup.Account.Employee.SubSectionCode = "SS1";
        await db.SaveChangesAsync();

        await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.Equal(("S1", "Infrastructure", "SS1", "Network"), (monthly.SectionCodeSnapshot, monthly.SectionNameSnapshot, monthly.SubSectionCodeSnapshot, monthly.SubSectionNameSnapshot));
    }

    [Fact]
    public async Task Match_leaves_a_roaming_bill_unassessed_when_nothing_is_remembered()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db);
        setup.Line.Roaming = 12.50m;
        await db.SaveChangesAsync();

        await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.Null(monthly.Responsibility);
        Assert.Null(monthly.Remark);
        Assert.Null(monthly.AssessedAt);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Match_applies_the_remembered_responsibility_to_a_roaming_bill()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db);
        setup.Line.Roaming = 12.50m;
        setup.Account.Employee.DefaultResponsibility = Responsibility.ByUser;
        await db.SaveChangesAsync();

        await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.Equal(Responsibility.ByUser, monthly.Responsibility);
        Assert.Equal(40m, monthly.FinalDeduction);
        Assert.Null(monthly.Remark);
        Assert.Equal("AutoAssessedFromPreviousBatch", (await db.AuditLogs.SingleAsync()).Action);
    }

    [Theory]
    [InlineData(Responsibility.ByCompany, 0)]
    [InlineData(Responsibility.ByUser, 40)]
    public async Task Match_auto_assesses_with_the_remembered_responsibility_and_its_default_deduction(Responsibility remembered, int expectedDeduction)
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db);
        setup.Account.Employee.DefaultResponsibility = remembered; await db.SaveChangesAsync();

        await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(setup.Batch.Id, default);

        var monthly = await db.MonthlyBills.SingleAsync();
        Assert.Equal(remembered, monthly.Responsibility);
        Assert.Equal(expectedDeduction, monthly.FinalDeduction);
        Assert.Equal(40m, monthly.CalculatedExcess);
        Assert.NotNull(monthly.AssessedAt);
        Assert.Equal("matcher", monthly.AssessedBy);
        Assert.Null(monthly.DeductionOverrideAmount);
        Assert.Equal("AutoAssessedFromPreviousBatch", (await db.AuditLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task Historical_override_also_applies_the_remembered_responsibility()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db, accountIsActive: false);
        setup.Account.Employee.DefaultResponsibility = Responsibility.ByCompany; await db.SaveChangesAsync();
        var exception = new BillException { BillBatchId = setup.Batch.Id, BillLineId = setup.Line.Id, ExceptionType = BillExceptionType.MOBILE_NOT_FOUND, Severity = BillExceptionSeverity.Blocking, Description = "not found" };
        db.BillExceptions.Add(exception); await db.SaveChangesAsync();

        await new EfBillExceptionReviewService(db, new FixedAuthorization(true), new TestUser("actual-reviewer"), new TestClock())
            .ResolveMobileNotFoundAsync(exception.Id, new ResolveMobileAccountExceptionRequest(setup.Account.Id, "historical ownership confirmed manually"), default);

        Assert.Equal(Responsibility.ByCompany, (await db.MonthlyBills.SingleAsync()).Responsibility);
        Assert.Contains(await db.AuditLogs.ToListAsync(), audit => audit.Action == "AutoAssessedFromPreviousBatch");
    }

    [Fact]
    public async Task Match_creates_mobile_not_found_and_zero_bill_exceptions_without_discarding_line()
    {
        await using var db = CreateDb();
        var batch = new BillBatch { ProviderId = Guid.NewGuid(), CorporateCode = "C", BillingYear = 2026, BillingMonth = 9, Status = BillBatchStatus.Validated };
        db.BillBatches.Add(batch);
        db.BillLines.Add(new BillLine { BillBatchId = batch.Id, MobileNumber = "768791861", PageNumber = 1, RawText = "zero", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 0m });
        await db.SaveChangesAsync();

        var result = await new EfBillMatchingService(db, new TestClock(), new TestUser("matcher"), new FixedAuthorization(true)).MatchAsync(batch.Id, default);

        Assert.Equal(0, result.MonthlyBillsCreated);
        Assert.Equal(2, result.ExceptionsCreated);
        Assert.Contains(await db.BillExceptions.ToListAsync(), x => x.ExceptionType == BillExceptionType.ZERO_BILL);
        Assert.Contains(await db.BillExceptions.ToListAsync(), x => x.ExceptionType == BillExceptionType.MOBILE_NOT_FOUND);
    }

    [Fact]
    public async Task Denied_manual_resolution_does_not_change_exception_or_create_audit()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db, accountIsActive: false);
        var exception = new BillException { BillBatchId = setup.Batch.Id, BillLineId = setup.Line.Id, ExceptionType = BillExceptionType.MOBILE_NOT_FOUND, Severity = BillExceptionSeverity.Blocking, Description = "not found" };
        db.BillExceptions.Add(exception); await db.SaveChangesAsync();
        var service = new EfBillExceptionReviewService(db, new FixedAuthorization(false), new TestUser("not-dev"), new TestClock());

        await Assert.ThrowsAsync<BillReviewForbiddenException>(() => service.ResolveMobileNotFoundAsync(exception.Id, new ResolveMobileAccountExceptionRequest(setup.Account.Id, "historical owner confirmed"), default));

        Assert.Equal(BillExceptionStatus.Open, (await db.BillExceptions.SingleAsync()).Status);
        Assert.Empty(await db.BillExceptionResolutions.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
        Assert.Empty(await db.MonthlyBills.ToListAsync());
    }

    [Fact]
    public async Task Authorized_historical_override_records_actual_user_and_does_not_change_the_allocation()
    {
        await using var db = CreateDb();
        var setup = await SeedAsync(db, accountIsActive: false);
        var exception = new BillException { BillBatchId = setup.Batch.Id, BillLineId = setup.Line.Id, ExceptionType = BillExceptionType.MOBILE_NOT_FOUND, Severity = BillExceptionSeverity.Blocking, Description = "not found" };
        db.BillExceptions.Add(exception); await db.SaveChangesAsync();
        var service = new EfBillExceptionReviewService(db, new FixedAuthorization(true), new TestUser("actual-reviewer"), new TestClock());

        var result = await service.ResolveMobileNotFoundAsync(exception.Id, new ResolveMobileAccountExceptionRequest(setup.Account.Id, "historical ownership confirmed manually"), default);

        var resolution = await db.BillExceptionResolutions.SingleAsync();
        Assert.NotNull(result.MonthlyBillId);
        Assert.Equal("actual-reviewer", resolution.ResolvedBy);
        Assert.False((await db.MobileAccounts.SingleAsync()).IsActive);
        Assert.Equal(AllocationMatchMethod.ManualHistoricalOverride, (await db.MonthlyBills.SingleAsync()).AllocationMatchMethod);
    }

    private static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<(BillBatch Batch, BillLine Line, MobileAccount Account)> SeedAsync(MobileBillDbContext db, bool accountIsActive = true)
    {
        var factory = new Factory { Code = "F", Name = "Factory" };
        var department = new Department { Code = "D", Name = "Department" };
        var category = new EmployeeCategory { Code = "C", Name = "Category" };
        var designation = new Designation { Code = "DS", Name = "Designation" };
        var employee = new Employee { EPF = "EPF-1", FullName = "Employee One", CategoryCode = category.Code, Category = category, DesignationCode = designation.Code, Designation = designation, FactoryCode = factory.Code, Factory = factory, DepartmentCode = department.Code, Department = department };
        var account = new MobileAccount { MobileNumber = "768791861", EmployeeId = employee.Id, Employee = employee, IsActive = accountIsActive, MonthlyCreditLimit = 100m, MonthlyRental = 10m };
        var batch = new BillBatch { ProviderId = Guid.NewGuid(), CorporateCode = "C", BillingYear = 2026, BillingMonth = 9, Status = BillBatchStatus.Validated };
        var line = new BillLine { BillBatchId = batch.Id, MobileNumber = account.MobileNumber, PageNumber = 1, RawText = "row", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 150m };
        db.AddRange(factory, department, category, designation, employee, account, batch, line); await db.SaveChangesAsync(); return (batch, line, account);
    }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero); }
    private sealed class TestUser(string id) : ICurrentUserService { public string UserId => id; public string DisplayName => id; public UserRole Role => UserRole.ITEngineer; }
    private sealed class FixedAuthorization(bool value) : IBillReviewAuthorizationService { public Task<bool> CanResolveExceptionsAsync(CancellationToken cancellationToken) => Task.FromResult(value); public Task<bool> AuthorizeAsync(BillWorkflowAction action, CancellationToken cancellationToken) => Task.FromResult(value); }
}
