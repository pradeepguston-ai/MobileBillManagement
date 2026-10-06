using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Application;

public sealed class SimPoolServiceTests
{
    private static readonly DateTimeOffset Today = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Resign_releases_the_employees_numbers_to_the_pool_and_deactivates_them()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (leaver, _) = await SeedEmployeesAsync(db);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000001", leaver.Id, 1000m, 100m), default);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000002", leaver.Id, 500m, 50m), default);

        var pooled = await service.ResignEmployeeAsync(leaver.Id, new ResignEmployeeRequest(new DateOnly(2026, 10, 3), null), default);

        Assert.Equal(["0771000001", "0771000002"], pooled.Select(item => item.MobileNumber));
        Assert.All(pooled, item => Assert.Equal((SimStatus.Pooled, true, new DateOnly(2026, 10, 3), "Resigned"), (item.Status, item.IsActive, item.PooledOn!.Value, item.StatusReason)));
        Assert.False((await db.Employees.SingleAsync(item => item.Id == leaver.Id)).IsActive);
        Assert.Equal(3, await db.AuditLogs.CountAsync());
        Assert.All(await db.AuditLogs.ToListAsync(), log => Assert.Equal("hr-user", log.PerformedBy));
    }

    [Fact]
    public async Task Employee_with_assigned_numbers_cannot_be_deactivated_until_they_are_pooled()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (leaver, _) = await SeedEmployeesAsync(db);
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000003", leaver.Id, 1000m, 100m), default);

        var blocked = await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DeactivateEmployeeAsync(leaver.Id, default));
        Assert.Contains("SIM Pool", blocked.Message);
        await service.ReleaseToPoolAsync(account.Id, new ReleaseToPoolRequest(new DateOnly(2026, 10, 1), "Resigned"), default);
        await service.DeactivateEmployeeAsync(leaver.Id, default);

        Assert.False((await db.Employees.SingleAsync(item => item.Id == leaver.Id)).IsActive);
    }

    [Fact]
    public async Task Assign_from_pool_closes_the_pool_period_and_starts_a_new_allocation()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (leaver, joiner) = await SeedEmployeesAsync(db);
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000004", leaver.Id, 1000m, 100m), default);
        await service.ReleaseToPoolAsync(account.Id, new ReleaseToPoolRequest(new DateOnly(2026, 8, 1), null), default);

        var assigned = await service.AssignFromPoolAsync(account.Id, new AssignFromPoolRequest(joiner.Id), default);

        Assert.NotEqual(account.Id, assigned.Id);
        Assert.Equal((joiner.EPF, SimStatus.Assigned, true, 1000m, 100m), (assigned.Epf, assigned.Status, assigned.IsActive, assigned.MonthlyCreditLimit, assigned.MonthlyRental));
        var closed = await db.MobileAccounts.SingleAsync(item => item.Id == account.Id);
        Assert.Equal((false, SimStatus.Pooled, leaver.Id), (closed.IsActive, closed.Status, closed.EmployeeId));
        Assert.Empty(await service.GetSimPoolAsync(default));
    }

    [Fact]
    public async Task Assign_from_pool_can_set_new_amounts()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (leaver, joiner) = await SeedEmployeesAsync(db);
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000005", leaver.Id, 1000m, 100m), default);
        await service.ReleaseToPoolAsync(account.Id, new ReleaseToPoolRequest(new DateOnly(2026, 8, 1), null), default);

        var assigned = await service.AssignFromPoolAsync(account.Id, new AssignFromPoolRequest(joiner.Id, 2500m, 250m), default);

        Assert.Equal((2500m, 250m), (assigned.MonthlyCreditLimit, assigned.MonthlyRental));
    }

    [Fact]
    public async Task Pooled_number_is_handled_only_through_the_pool_actions()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (leaver, joiner) = await SeedEmployeesAsync(db);
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000006", leaver.Id, 1000m, 100m), default);

        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.AssignFromPoolAsync(account.Id, new AssignFromPoolRequest(joiner.Id), default));
        await service.ReleaseToPoolAsync(account.Id, new ReleaseToPoolRequest(new DateOnly(2026, 8, 1), null), default);

        var duplicate = await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000006", joiner.Id, 1000m, 100m), default));
        Assert.Contains("SIM Pool", duplicate.Message);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.ReassignMobileAccountAsync(account.Id, new MobileAccountReassignRequest(joiner.Id), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.ReleaseToPoolAsync(account.Id, new ReleaseToPoolRequest(new DateOnly(2026, 8, 1), null), default));
    }

    [Fact]
    public async Task Disconnect_takes_the_number_out_of_the_pool_and_frees_it()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (leaver, joiner) = await SeedEmployeesAsync(db);
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000007", leaver.Id, 1000m, 100m), default);
        await service.ReleaseToPoolAsync(account.Id, new ReleaseToPoolRequest(new DateOnly(2026, 8, 1), null), default);

        var disconnected = await service.DisconnectSimAsync(account.Id, new DisconnectSimRequest(new DateOnly(2026, 10, 4), "Not needed"), default);

        Assert.Equal((SimStatus.Disconnected, false, new DateOnly(2026, 10, 4), "Not needed"), (disconnected.Status, disconnected.IsActive, disconnected.DisconnectedOn!.Value, disconnected.StatusReason));
        Assert.Empty(await service.GetSimPoolAsync(default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DisconnectSimAsync(account.Id, new DisconnectSimRequest(new DateOnly(2026, 10, 4), null), default));
    }

    [Fact]
    public async Task Pool_lists_days_in_pool_and_flags_numbers_idle_over_60_days()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (leaver, _) = await SeedEmployeesAsync(db);
        var old = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000008", leaver.Id, 1000m, 100m), default);
        var recent = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000009", leaver.Id, 1000m, 100m), default);
        await service.ReleaseToPoolAsync(old.Id, new ReleaseToPoolRequest(new DateOnly(2026, 7, 1), null), default);
        await service.ReleaseToPoolAsync(recent.Id, new ReleaseToPoolRequest(new DateOnly(2026, 9, 20), null), default);

        var pool = await service.GetSimPoolAsync(default);

        Assert.Equal([("0771000008", 96, true), ("0771000009", 15, false)], pool.Select(item => (item.MobileNumber, item.DaysInPool, item.IsLongIdle)));
        Assert.All(pool, item => Assert.Equal((leaver.EPF, "Leaver"), (item.PreviousEpf, item.PreviousEmployeeName)));
    }

    private static EfMasterDataService Service(MobileBillDbContext db) => new(db, new TestUser(), new TestClock());

    private static MobileBillDbContext CreateContext() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Employee Leaver, Employee Joiner)> SeedEmployeesAsync(MobileBillDbContext context)
    {
        var factory = new Factory { Code = "F", Name = "Factory" }; var department = new Department { Code = "D", Name = "Department" };
        var category = new EmployeeCategory { Code = "C", Name = "Category" }; var designation = new Designation { Code = "DS", Name = "Designation" };
        var leaver = new Employee { EPF = "EPF-L", FullName = "Leaver", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        var joiner = new Employee { EPF = "EPF-J", FullName = "Joiner", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        context.AddRange(factory, department, category, designation, leaver, joiner);
        await context.SaveChangesAsync();
        return (leaver, joiner);
    }

    private sealed class TestUser : ICurrentUserService { public string UserId => "hr-user"; public string DisplayName => "HR User"; public UserRole Role => UserRole.HrUser; }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Today; }
}
