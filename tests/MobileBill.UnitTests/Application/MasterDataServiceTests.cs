using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Application;

public sealed class MasterDataServiceTests
{
    [Fact]
    public async Task Factories_support_create_search_pagination_and_status_filtering()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await service.CreateFactoryAsync(new ReferenceDataUpsertRequest("HQ", "Head Office"), default);
        await service.CreateFactoryAsync(new ReferenceDataUpsertRequest("PL", "Plant"), default);
        var page = await service.GetFactoriesAsync(new PagedRequest(Search: "Head", PageSize: 1), default);
        Assert.Single(page.Items); Assert.Equal("HQ", page.Items[0].Code); Assert.Equal(1, page.TotalCount);
        await service.DeactivateFactoryAsync(page.Items[0].Id, default);
        var inactive = await service.GetFactoriesAsync(new PagedRequest(IsActive: false), default);
        Assert.Single(inactive.Items);
    }

    [Fact]
    public async Task Mobile_account_rejects_a_second_active_allocation_for_the_same_number()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234567", employee.Id, 1000m, 100m, TestPackages.PackageId), default);
        var exception = await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateMobileAccountAsync(new MobileAccountUpsertRequest(" 0771234567 ", employee.Id, 1000m, 100m, TestPackages.PackageId), default));
        Assert.Contains("already has an active allocation", exception.Message);
    }

    [Fact]
    public async Task Mobile_account_number_can_be_allocated_again_after_the_previous_allocation_is_deactivated()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);
        var first = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234567", employee.Id, 1000m, 100m, TestPackages.PackageId), default);
        await service.DeactivateMobileAccountAsync(first.Id, default);

        var second = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234567", employee.Id, 1000m, 100m, TestPackages.PackageId), default);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Mobile_account_updates_both_monthly_amounts()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234568", employee.Id, 1000m, 100m, TestPackages.PackageId), default);

        var updated = await service.UpdateMobileAccountAsync(account.Id, new MobileAccountUpsertRequest("0771234568", employee.Id, 1500.25m, 200m, TestPackages.PackageId), default);

        Assert.Equal(1500.25m, updated.MonthlyCreditLimit);
        Assert.Equal(200m, updated.MonthlyRental);
        Assert.Equal(1500.25m, (await context.MobileAccounts.SingleAsync()).MonthlyCreditLimit);
    }
    [Fact]
    public async Task Mobile_account_rejects_fractional_cents()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);

        await Assert.ThrowsAsync<MasterDataValidationException>(() =>
            service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234570", employee.Id, 1000.001m, 100m, TestPackages.PackageId), default));
        Assert.Empty(context.MobileAccounts);
    }

    [Fact]
    public async Task Employee_deactivation_does_not_cascade_and_rejects_invalid_current_state()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234569", employee.Id, 1000m, 100m, TestPackages.PackageId), default);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DeactivateEmployeeAsync(employee.Id, default));
        Assert.True((await service.GetEmployeeAsync(employee.Id, default)).IsActive);
    }

    [Fact]
    public async Task Missing_record_returns_not_found()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await Assert.ThrowsAsync<MasterDataNotFoundException>(() => service.GetEmployeeAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Reassign_moves_the_number_to_another_employee_and_keeps_the_amounts()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var first = await SeedEmployeeAsync(context);
        var second = new Employee { EPF = "EPF-2", FullName = "Second Employee", CategoryCode = first.CategoryCode, DesignationCode = first.DesignationCode, FactoryCode = first.FactoryCode, DepartmentCode = first.DepartmentCode };
        context.Employees.Add(second); await context.SaveChangesAsync();
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234570", first.Id, 1200m, 300m, TestPackages.PackageId), default);

        var moved = await service.ReassignMobileAccountAsync(account.Id, new MobileAccountReassignRequest(second.Id), default);

        Assert.Equal(("EPF-2", "0771234570", 1200m, 300m), (moved.Epf, moved.MobileNumber, moved.MonthlyCreditLimit, moved.MonthlyRental));
        Assert.Equal(account.Id, moved.Id);
    }

    [Fact]
    public async Task Reassign_requires_an_active_allocation_and_an_active_different_employee()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234571", employee.Id, 1000m, 100m, TestPackages.PackageId), default);

        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.ReassignMobileAccountAsync(account.Id, new MobileAccountReassignRequest(employee.Id), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.ReassignMobileAccountAsync(account.Id, new MobileAccountReassignRequest(Guid.NewGuid()), default));
        await service.DeactivateMobileAccountAsync(account.Id, default);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.ReassignMobileAccountAsync(account.Id, new MobileAccountReassignRequest(employee.Id), default));
    }

    [Fact]
    public async Task Same_epf_is_allowed_in_different_factories_but_not_twice_in_one_factory()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var existing = await SeedEmployeeAsync(context);
        context.Add(new Factory { Code = "F2", Name = "Factory Two" }); await context.SaveChangesAsync();

        var otherFactory = await service.CreateEmployeeAsync(new EmployeeUpsertRequest(existing.EPF, "Other Person", null, "C", "DS", "F2", "D"), default);
        var duplicate = await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateEmployeeAsync(new EmployeeUpsertRequest(existing.EPF, "Same Factory", null, "C", "DS", "F", "D"), default));
        var moveIntoClash = await Assert.ThrowsAsync<MasterDataConflictException>(() => service.UpdateEmployeeAsync(otherFactory.Id, new EmployeeUpsertRequest(existing.EPF, "Other Person", null, "C", "DS", "F", "D"), default));

        Assert.Equal((existing.EPF, "F2"), (otherFactory.Epf, otherFactory.FactoryCode));
        Assert.Contains("selected factory", duplicate.Message);
        Assert.Contains("selected factory", moveIntoClash.Message);
    }

    [Fact]
    public async Task Allocations_follow_the_employee_record_not_the_epf_number()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var first = await SeedEmployeeAsync(context);
        context.Add(new Factory { Code = "F2", Name = "Factory Two" }); await context.SaveChangesAsync();
        var twin = await service.CreateEmployeeAsync(new EmployeeUpsertRequest(first.EPF, "Twin In Other Factory", null, "C", "DS", "F2", "D"), default);

        var allocation = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234580", twin.Id, 1000m, 100m, TestPackages.PackageId), default);

        Assert.Equal((twin.Id, "Twin In Other Factory", "Factory Two"), (allocation.EmployeeId, allocation.EmployeeName, allocation.Factory));
        await service.DeactivateEmployeeAsync(first.Id, default);   // the namesake in factory F holds no number
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DeactivateEmployeeAsync(twin.Id, default));
    }

    private static MobileBillDbContext CreateContext() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<Employee> SeedEmployeeAsync(MobileBillDbContext context)
    {
        var factory = new Factory { Code = "F", Name = "Factory" }; var department = new Department { Code = "D", Name = "Department" }; var category = new EmployeeCategory { Code = "C", Name = "Category" }; var designation = new Designation { Code = "DS", Name = "Designation" };
        context.AddRange(factory, department, category, designation); TestPackages.Add(context); await context.SaveChangesAsync();
        var employee = new Employee { EPF = Guid.NewGuid().ToString("N"), FullName = "Test Employee", CategoryCode = category.Code, DesignationCode = designation.Code, FactoryCode = factory.Code, DepartmentCode = department.Code }; context.Employees.Add(employee); await context.SaveChangesAsync(); return employee;
    }
}
