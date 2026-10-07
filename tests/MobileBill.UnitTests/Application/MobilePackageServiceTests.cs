using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Application;

public sealed class MobilePackageServiceTests
{
    [Fact]
    public async Task Package_is_created_with_its_amounts_and_listed_with_allocation_counts()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (provider, employee) = await SeedAsync(db);

        var package = await service.CreatePackageAsync(new MobilePackageUpsertRequest(" PPU23_700 ", provider.Id, "Unlimited any network calls & 5 GB data per month", 700m, 940m, 1000m), default);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000001", employee.Id, 2000m, 700m, package.Id), default);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000002", employee.Id, 1000m, 650m, package.Id), default);

        var listed = Assert.Single((await service.GetPackagesAsync(new PagedRequest(Search: "5 GB"), default)).Items);
        Assert.Equal(("PPU23_700", "Dialog", 700m, 940m, 1000m, true), (listed.Code, listed.ProviderName, listed.MonthlyRental, listed.TotalWithTax, listed.DefaultCreditLimit, listed.IsActive));
        Assert.Equal((2, 1), (listed.AllocationCount, listed.AllocationsWithOtherRental));
        Assert.Contains(await db.AuditLogs.ToListAsync(), log => log.Action == "MobilePackageCreated" && log.PerformedBy == "it-user");
    }

    [Fact]
    public async Task Package_code_is_unique_per_provider_and_amounts_are_checked()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (provider, _) = await SeedAsync(db);
        var other = new TelecomProvider { Code = "P02", Name = "Mobitel" }; db.Add(other); await db.SaveChangesAsync();
        await service.CreatePackageAsync(new MobilePackageUpsertRequest("PPU23_700", provider.Id, "Plan", 700m, 940m, 1000m), default);

        await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreatePackageAsync(new MobilePackageUpsertRequest("PPU23_700", provider.Id, "Again", 700m, 940m, 1000m), default));
        await service.CreatePackageAsync(new MobilePackageUpsertRequest("PPU23_700", other.Id, "Same code, other provider", 700m, 940m, 1000m), default);
        var lower = await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreatePackageAsync(new MobilePackageUpsertRequest("X", provider.Id, "Plan", 700m, 600m, 1000m), default));
        Assert.Contains("Total with Tax", lower.Message);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreatePackageAsync(new MobilePackageUpsertRequest("X", provider.Id, "Plan", 700.001m, 940m, 1000m), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreatePackageAsync(new MobilePackageUpsertRequest("X", Guid.NewGuid(), "Plan", 700m, 940m, 1000m), default));
    }

    [Fact]
    public async Task A_new_allocation_needs_an_active_package_and_cannot_drop_it_later()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (provider, employee) = await SeedAsync(db);
        var package = await service.CreatePackageAsync(new MobilePackageUpsertRequest("PPU23_700", provider.Id, "Plan", 700m, 940m, 1000m), default);
        var retired = await service.CreatePackageAsync(new MobilePackageUpsertRequest("OLD", provider.Id, "Old plan", 500m, 600m, 500m), default);
        await service.DeactivatePackageAsync(retired.Id, default);

        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000003", employee.Id, 1000m, 700m), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000003", employee.Id, 1000m, 700m, retired.Id), default));
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000003", employee.Id, 1000m, 700m, package.Id, SimType.ESim), default);
        Assert.Equal(("PPU23_700", false, SimType.ESim), (account.PackageCode, account.DiffersFromPackage, account.SimType!.Value));

        var removed = await Assert.ThrowsAsync<MasterDataValidationException>(() => service.UpdateMobileAccountAsync(account.Id, new MobileAccountUpsertRequest("0771000003", employee.Id, 1000m, 700m), default));
        Assert.Contains("cannot be removed", removed.Message);
        var overridden = await service.UpdateMobileAccountAsync(account.Id, new MobileAccountUpsertRequest("0771000003", employee.Id, 3000m, 800m, package.Id, SimType.VoiceData), default);
        Assert.Equal((3000m, 800m, true, SimType.VoiceData), (overridden.MonthlyCreditLimit, overridden.MonthlyRental, overridden.DiffersFromPackage, overridden.SimType!.Value));
    }

    [Fact]
    public async Task An_older_allocation_without_a_package_can_still_be_edited_and_given_one()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (provider, employee) = await SeedAsync(db);
        var package = await service.CreatePackageAsync(new MobilePackageUpsertRequest("PPU23_700", provider.Id, "Plan", 700m, 940m, 1000m), default);
        var old = new MobileAccount { MobileNumber = "0771000004", EmployeeId = employee.Id, MonthlyCreditLimit = 1000m, MonthlyRental = 700m };
        db.Add(old); await db.SaveChangesAsync();

        var edited = await service.UpdateMobileAccountAsync(old.Id, new MobileAccountUpsertRequest("0771000004", employee.Id, 1500m, 700m), default);
        Assert.Null(edited.PackageCode);
        var given = await service.UpdateMobileAccountAsync(old.Id, new MobileAccountUpsertRequest("0771000004", employee.Id, 1500m, 700m, package.Id), default);
        Assert.Equal("PPU23_700", given.PackageCode);
    }

    [Fact]
    public async Task Changing_a_package_leaves_allocations_until_its_rental_is_applied()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (provider, employee) = await SeedAsync(db);
        var package = await service.CreatePackageAsync(new MobilePackageUpsertRequest("PPU23_700", provider.Id, "Plan", 700m, 940m, 1000m), default);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000005", employee.Id, 2000m, 700m, package.Id), default);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000006", employee.Id, 1500m, 700m, package.Id), default);

        await service.UpdatePackageAsync(package.Id, new MobilePackageUpsertRequest("PPU23_700", provider.Id, "Plan", 750m, 1007m, 1000m), default);
        Assert.All(await db.MobileAccounts.ToListAsync(), account => Assert.Equal(700m, account.MonthlyRental));

        Assert.Equal(2, await service.ApplyPackageRentalAsync(package.Id, default));
        Assert.All(await db.MobileAccounts.ToListAsync(), account => Assert.Equal(750m, account.MonthlyRental));
        Assert.Equal([1500m, 2000m], (await db.MobileAccounts.Select(account => account.MonthlyCreditLimit).ToListAsync()).Order());   // credit limits stay per person
        Assert.Equal(2, await db.AuditLogs.CountAsync(log => log.Action == "PackageRentalApplied"));
        Assert.Equal(0, await service.ApplyPackageRentalAsync(package.Id, default));
    }

    [Fact]
    public async Task Assign_from_pool_keeps_the_package_unless_a_new_one_is_chosen()
    {
        await using var db = CreateContext(); var service = Service(db);
        var (provider, employee) = await SeedAsync(db);
        var joiner = new Employee { EPF = "EPF-J", FullName = "Joiner", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        db.Add(joiner); await db.SaveChangesAsync();
        var package = await service.CreatePackageAsync(new MobilePackageUpsertRequest("PPU23_700", provider.Id, "Plan", 700m, 940m, 1000m), default);
        var bigger = await service.CreatePackageAsync(new MobilePackageUpsertRequest("PPU23_1500", provider.Id, "Bigger plan", 1500m, 2014m, 2000m), default);
        var first = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000007", employee.Id, 1000m, 700m, package.Id, SimType.Voice), default);
        var second = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771000008", employee.Id, 1000m, 700m, package.Id), default);
        await service.ReleaseToPoolAsync(first.Id, new ReleaseToPoolRequest(new DateOnly(2026, 9, 1), null), default);
        await service.ReleaseToPoolAsync(second.Id, new ReleaseToPoolRequest(new DateOnly(2026, 9, 1), null), default);

        var kept = await service.AssignFromPoolAsync(first.Id, new AssignFromPoolRequest(joiner.Id), default);
        var changed = await service.AssignFromPoolAsync(second.Id, new AssignFromPoolRequest(joiner.Id, MonthlyRental: 1500m, PackageId: bigger.Id), default);

        Assert.Equal(("PPU23_700", SimType.Voice), (kept.PackageCode, kept.SimType!.Value));   // the SIM keeps its type for the new holder
        Assert.Equal(("PPU23_1500", 1500m, SimStatus.Assigned), (changed.PackageCode, changed.MonthlyRental, changed.Status));
    }

    private static EfMasterDataService Service(MobileBillDbContext db) => new(db, new TestUser(), new TestClock());

    private static MobileBillDbContext CreateContext() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(TelecomProvider Provider, Employee Employee)> SeedAsync(MobileBillDbContext context)
    {
        var provider = new TelecomProvider { Code = "P01", Name = "Dialog" };
        var employee = new Employee { EPF = "EPF-1", FullName = "Holder", CategoryCode = "C", DesignationCode = "DS", FactoryCode = "F", DepartmentCode = "D" };
        context.AddRange(provider, new Factory { Code = "F", Name = "Factory" }, new Department { Code = "D", Name = "Department" },
            new EmployeeCategory { Code = "C", Name = "Category" }, new Designation { Code = "DS", Name = "Designation" }, employee);
        await context.SaveChangesAsync();
        return (provider, employee);
    }

    private sealed class TestUser : ICurrentUserService { public string UserId => "it-user"; public string DisplayName => "IT User"; public UserRole Role => UserRole.ITEngineer; }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero); }
}
