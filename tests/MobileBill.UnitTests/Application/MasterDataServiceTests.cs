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
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234567", employee.EPF, 1000m, 100m), default);
        var exception = await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateMobileAccountAsync(new MobileAccountUpsertRequest(" 0771234567 ", employee.EPF, 1000m, 100m), default));
        Assert.Contains("already has an active allocation", exception.Message);
    }

    [Fact]
    public async Task Mobile_account_number_can_be_allocated_again_after_the_previous_allocation_is_deactivated()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);
        var first = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234567", employee.EPF, 1000m, 100m), default);
        await service.DeactivateMobileAccountAsync(first.Id, default);

        var second = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234567", employee.EPF, 1000m, 100m), default);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Mobile_account_updates_both_monthly_amounts()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);
        var account = await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234568", employee.EPF, 1000m, 100m), default);

        var updated = await service.UpdateMobileAccountAsync(account.Id, new MobileAccountUpsertRequest("0771234568", employee.EPF, 1500.25m, 200m), default);

        Assert.Equal(1500.25m, updated.MonthlyCreditLimit);
        Assert.Equal(200m, updated.MonthlyRental);
        Assert.Equal(1500.25m, (await context.MobileAccounts.SingleAsync()).MonthlyCreditLimit);
    }
    [Fact]
    public async Task Mobile_account_rejects_fractional_cents()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);

        await Assert.ThrowsAsync<MasterDataValidationException>(() =>
            service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234570", employee.EPF, 1000.001m, 100m), default));
        Assert.Empty(context.MobileAccounts);
    }

    [Fact]
    public async Task Employee_deactivation_does_not_cascade_and_rejects_invalid_current_state()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var employee = await SeedEmployeeAsync(context);
        await service.CreateMobileAccountAsync(new MobileAccountUpsertRequest("0771234569", employee.EPF, 1000m, 100m), default);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DeactivateEmployeeAsync(employee.Id, default));
        Assert.True((await service.GetEmployeeAsync(employee.Id, default)).IsActive);
    }

    [Fact]
    public async Task Missing_record_returns_not_found()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await Assert.ThrowsAsync<MasterDataNotFoundException>(() => service.GetEmployeeAsync(Guid.NewGuid(), default));
    }

    private static MobileBillDbContext CreateContext() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<Employee> SeedEmployeeAsync(MobileBillDbContext context)
    {
        var factory = new Factory { Code = "F", Name = "Factory" }; var department = new Department { Code = "D", Name = "Department" }; var category = new EmployeeCategory { Code = "C", Name = "Category" }; var designation = new Designation { Code = "DS", Name = "Designation" };
        context.AddRange(factory, department, category, designation); await context.SaveChangesAsync();
        var employee = new Employee { EPF = Guid.NewGuid().ToString("N"), FullName = "Test Employee", CategoryCode = category.Code, DesignationCode = designation.Code, FactoryCode = factory.Code, DepartmentCode = department.Code }; context.Employees.Add(employee); await context.SaveChangesAsync(); return employee;
    }
}
