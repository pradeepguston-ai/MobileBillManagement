using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Application;

public sealed class ResignationServiceTests
{
    [Fact]
    public async Task A_future_resignation_is_pending_and_keeps_the_employee_and_their_numbers()
    {
        await using var db = CreateContext(); var clock = new TestClock(); var service = Service(db, clock);
        var (leaver, _) = await SeedEmployeesAsync(db);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000001", leaver.Id, 1000m, 100m, TestPackages.PackageId), default);

        var pooled = await service.ResignEmployeeAsync(leaver.Id, new ResignEmployeeRequest(new DateOnly(2026, 10, 31), "Notice given"), default);

        Assert.Empty(pooled);
        var employee = await db.Employees.SingleAsync(item => item.Id == leaver.Id);
        Assert.Equal((true, new DateOnly(2026, 10, 31), "Notice given"), (employee.IsActive, employee.ResignedOn!.Value, employee.ResignationReason));
        Assert.Equal(SimStatus.Assigned, (await db.MobileAccounts.SingleAsync()).Status);
        var pending = Assert.Single(await service.GetResignationsAsync(ResignationStatus.Pending, default));
        Assert.Equal(("EPF-L", 26, "0771000001"), (pending.Epf, pending.DaysLeft, Assert.Single(pending.MobileNumbers)));
        Assert.Empty(await service.GetResignationsAsync(ResignationStatus.Resigned, default));
        Assert.Contains(await db.AuditLogs.ToListAsync(), log => log.Action == "EmployeeResignationScheduled" && log.PerformedBy == "hr-user");
    }

    [Fact]
    public async Task On_its_date_a_pending_resignation_is_completed_by_the_system()
    {
        await using var db = CreateContext(); var clock = new TestClock(); var service = Service(db, clock);
        var (leaver, _) = await SeedEmployeesAsync(db);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000002", leaver.Id, 1000m, 100m, TestPackages.PackageId), default);
        await service.ResignEmployeeAsync(leaver.Id, new ResignEmployeeRequest(new DateOnly(2026, 10, 10), "Notice given"), default);

        Assert.Equal(0, await service.CompleteDueResignationsAsync(default));         // 5 Oct: not yet
        clock.UtcNow = new DateTimeOffset(2026, 10, 10, 0, 30, 0, TimeSpan.Zero);
        Assert.Equal(1, await service.CompleteDueResignationsAsync(default));

        var employee = await db.Employees.SingleAsync(item => item.Id == leaver.Id);
        Assert.False(employee.IsActive);
        var account = await db.MobileAccounts.SingleAsync();
        Assert.Equal((SimStatus.Pooled, new DateOnly(2026, 10, 10), "Notice given"), (account.Status, account.PooledOn!.Value, account.StatusReason));
        Assert.All(await db.AuditLogs.Where(log => log.Action == "EmployeeResigned" || log.Action == "ReleasedToSimPool").ToListAsync(), log => Assert.Equal("system", log.PerformedBy));
        var resigned = Assert.Single(await service.GetResignationsAsync(ResignationStatus.Resigned, default));
        Assert.Equal((new DateOnly(2026, 10, 10), 0, "0771000002"), (resigned.ResignedOn, resigned.DaysLeft, Assert.Single(resigned.MobileNumbers)));
        Assert.Equal(0, await service.CompleteDueResignationsAsync(default));
    }

    [Fact]
    public async Task Listing_completes_resignations_that_have_become_due()
    {
        await using var db = CreateContext(); var clock = new TestClock(); var service = Service(db, clock);
        var (leaver, _) = await SeedEmployeesAsync(db);
        await service.ResignEmployeeAsync(leaver.Id, new ResignEmployeeRequest(new DateOnly(2026, 10, 12), null), default);
        clock.UtcNow = new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

        Assert.Empty(await service.GetResignationsAsync(ResignationStatus.Pending, default));
        Assert.Equal("Resigned", Assert.Single(await service.GetResignationsAsync(ResignationStatus.Resigned, default)).Reason);
    }

    [Fact]
    public async Task A_pending_resignation_can_be_moved_cancelled_or_completed_now()
    {
        await using var db = CreateContext(); var clock = new TestClock(); var service = Service(db, clock);
        var (leaver, joiner) = await SeedEmployeesAsync(db);
        await service.ResignEmployeeAsync(leaver.Id, new ResignEmployeeRequest(new DateOnly(2026, 10, 31), null), default);
        await service.ResignEmployeeAsync(joiner.Id, new ResignEmployeeRequest(new DateOnly(2026, 11, 15), null), default);

        await service.ResignEmployeeAsync(joiner.Id, new ResignEmployeeRequest(new DateOnly(2026, 11, 30), "Moved date"), default);
        await service.CancelResignationAsync(leaver.Id, default);

        var stays = await db.Employees.SingleAsync(item => item.Id == leaver.Id);
        Assert.Equal((true, (DateOnly?)null, (string?)null), (stays.IsActive, stays.ResignedOn, stays.ResignationReason));
        Assert.Equal(new DateOnly(2026, 11, 30), Assert.Single(await service.GetResignationsAsync(ResignationStatus.Pending, default)).ResignedOn);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CancelResignationAsync(leaver.Id, default));

        await service.ResignEmployeeAsync(joiner.Id, new ResignEmployeeRequest(new DateOnly(2026, 10, 5), null), default);  // today: completes now
        Assert.False((await db.Employees.SingleAsync(item => item.Id == joiner.Id)).IsActive);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CancelResignationAsync(joiner.Id, default));
    }

    [Fact]
    public async Task Reason_is_limited_to_250_characters()
    {
        await using var db = CreateContext(); var service = Service(db, new TestClock());
        var (leaver, _) = await SeedEmployeesAsync(db);

        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.ResignEmployeeAsync(leaver.Id, new ResignEmployeeRequest(new DateOnly(2026, 10, 31), new string('x', 251)), default));
    }

    private static EfMasterDataService Service(MobileBillDbContext db, IClock clock) => new(db, new TestUser(), clock);

    private static MobileBillDbContext CreateContext() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Employee Leaver, Employee Joiner)> SeedEmployeesAsync(MobileBillDbContext context)
    {
        var factory = new Factory { Code = "F", Name = "Factory" }; var department = new Department { Code = "D", Name = "Department" };
        var category = new EmployeeCategory { Code = "C", Name = "Category" }; var designation = new Designation { Code = "DS", Name = "Designation" };
        var leaver = new Employee { EPF = "EPF-L", FullName = "Leaver", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        var joiner = new Employee { EPF = "EPF-J", FullName = "Joiner", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        context.AddRange(factory, department, category, designation, leaver, joiner); TestPackages.Add(context);
        await context.SaveChangesAsync();
        return (leaver, joiner);
    }

    private sealed class TestUser : ICurrentUserService { public string UserId => "hr-user"; public string DisplayName => "HR User"; public UserRole Role => UserRole.HrUser; }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero); }
}
