using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
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
    public async Task Departments_designations_and_categories_get_generated_codes_and_unique_names()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        context.Add(new Department { Code = "IT", Name = "Typed Before" }); await context.SaveChangesAsync();

        var hr = await service.CreateDepartmentAsync(new ReferenceDataUpsertRequest(null, "  Human   Resources "), default);
        var finance = await service.CreateDepartmentAsync(new ReferenceDataUpsertRequest("", "Finance"), default);
        var designation = await service.CreateDesignationAsync(new ReferenceDataUpsertRequest(null, "Engineer"), default);
        var category = await service.CreateCategoryAsync(new ReferenceDataUpsertRequest(null, "Staff"), default);

        Assert.Equal(("DEP001", "Human Resources"), (hr.Code, hr.Name));
        Assert.Equal(("DEP002", "DES001", "CAT001"), (finance.Code, designation.Code, category.Code));
        var duplicate = await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateDepartmentAsync(new ReferenceDataUpsertRequest(null, "human resources"), default));
        Assert.Equal("A department named 'human resources' already exists.", duplicate.Message);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateDepartmentAsync(new ReferenceDataUpsertRequest(null, "  "), default));

        // Renaming keeps the code; a name taken by another department is refused, its own name is fine.
        var renamed = await service.UpdateDepartmentAsync(hr.Id, new ReferenceDataUpsertRequest(null, "People & Culture"), default);
        Assert.Equal(("DEP001", "People & Culture"), (renamed.Code, renamed.Name));
        await Assert.ThrowsAsync<MasterDataConflictException>(() => service.UpdateDepartmentAsync(hr.Id, new ReferenceDataUpsertRequest(null, "Finance"), default));
        await service.UpdateDepartmentAsync(finance.Id, new ReferenceDataUpsertRequest(null, "FINANCE"), default);

        var dialog = await service.CreateProviderAsync(new ReferenceDataUpsertRequest(null, "Dialog"), default);
        Assert.Equal(("PRV001", "Dialog"), (dialog.Code, dialog.Name));
        await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateProviderAsync(new ReferenceDataUpsertRequest(null, "DIALOG"), default));

        // Factories keep the codes people give them.
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateFactoryAsync(new ReferenceDataUpsertRequest(null, "No Code"), default));
    }

    [Fact]
    public async Task Section_and_sub_section_names_are_unique_within_their_parent_and_codes_are_generated()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        var it = await service.CreateDepartmentAsync(new ReferenceDataUpsertRequest(null, "IT"), default);
        var hr = await service.CreateDepartmentAsync(new ReferenceDataUpsertRequest(null, "HR"), default);

        var itAdmin = await service.CreateSectionAsync(new SectionUpsertRequest(null, "Administration", it.Code), default);
        var hrAdmin = await service.CreateSectionAsync(new SectionUpsertRequest(null, "Administration", hr.Code), default);   // same name, other department
        var team = await service.CreateSubSectionAsync(new SubSectionUpsertRequest(null, "Team A", itAdmin.Code), default);
        await service.CreateSubSectionAsync(new SubSectionUpsertRequest(null, "Team A", hrAdmin.Code), default);

        Assert.Equal(("SEC001", "SEC002", "SUB001"), (itAdmin.Code, hrAdmin.Code, team.Code));
        await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateSectionAsync(new SectionUpsertRequest(null, "administration", it.Code), default));
        await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateSubSectionAsync(new SubSectionUpsertRequest(null, "team a", itAdmin.Code), default));
        var renamed = await service.UpdateSectionAsync(itAdmin.Id, new SectionUpsertRequest(null, "Admin", it.Code), default);
        Assert.Equal(("SEC001", "Admin"), (renamed.Code, renamed.Name));
    }

    [Fact]
    public async Task An_employee_needs_a_gender_and_gets_the_matching_title()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var existing = await SeedEmployeeAsync(context);

        var missing = await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateEmployeeAsync(new EmployeeUpsertRequest("EPF-9", "No Gender", null, "C", "DS", "F", "D"), default));
        Assert.Equal("Gender is required.", missing.Message);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateEmployeeAsync(new EmployeeUpsertRequest("EPF-9", "Bad Gender", null, "C", "DS", "F", "D", Gender: (Gender)7), default));
        var female = await service.CreateEmployeeAsync(new EmployeeUpsertRequest("EPF-9", "Nimali Perera", null, "C", "DS", "F", "D", Gender: Gender.Female, NameWithInitials: " N. PERERA "), default);
        Assert.Equal((Gender.Female, "MS.", "N. PERERA"), (female.Gender, female.Title, female.NameWithInitials));
        Assert.Single((await service.GetEmployeesAsync(new PagedRequest(Search: "N. PER"), default)).Items);   // found by initials too

        var changed = await service.UpdateEmployeeAsync(female.Id, new EmployeeUpsertRequest("EPF-9", "Nimal Perera", null, "C", "DS", "F", "D", Gender: Gender.Male), default);
        Assert.Equal((Gender.Male, "MR.", null), (changed.Gender, changed.Title, changed.NameWithInitials));   // left blank: cleared
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.UpdateEmployeeAsync(existing.Id, new EmployeeUpsertRequest(existing.EPF, "Test Employee", null, "C", "DS", "F", "D"), default));
        Assert.Null((await service.GetEmployeeAsync(existing.Id, default)).Title);   // an older record without a gender has no title
    }

    [Fact]
    public async Task Only_a_third_party_category_employee_can_have_a_third_party_company()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); await SeedEmployeeAsync(context);
        context.Add(new EmployeeCategory { Code = "TP", Name = "OTHER - THIRD-PARTY" }); await context.SaveChangesAsync();
        var avengers = await service.CreateThirdPartyCompanyAsync(new ReferenceDataUpsertRequest(null, "AVENGERS SECURITY & INVESTIGATIONS (PVT) LTD"), default);
        var closed = await service.CreateThirdPartyCompanyAsync(new ReferenceDataUpsertRequest(null, "Closed Agency"), default);
        await service.DeactivateThirdPartyCompanyAsync(closed.Id, default);
        Assert.Equal("TPC001", avengers.Code);

        var guard = await service.CreateEmployeeAsync(new EmployeeUpsertRequest("SEC_01", "Guard", null, "TP", "DS", "F", "D", Gender: Gender.Male, ThirdPartyCompanyCode: avengers.Code), default);
        Assert.Equal((avengers.Code, "AVENGERS SECURITY & INVESTIGATIONS (PVT) LTD"), (guard.ThirdPartyCompanyCode, guard.ThirdPartyCompanyName));
        var notApplicable = await service.CreateEmployeeAsync(new EmployeeUpsertRequest("SEC_02", "Own Guard", null, "TP", "DS", "F", "D", Gender: Gender.Male, ThirdPartyCompanyCode: ""), default);
        Assert.Null(notApplicable.ThirdPartyCompanyCode);   // optional even for third-party staff

        var outside = await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateEmployeeAsync(new EmployeeUpsertRequest("X1", "Clerk", null, "C", "DS", "F", "D", Gender: Gender.Male, ThirdPartyCompanyCode: avengers.Code), default));
        Assert.Equal("Third Party Allocation is only for the OTHER - THIRD-PARTY category.", outside.Message);
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateEmployeeAsync(new EmployeeUpsertRequest("SEC_03", "Guard", null, "TP", "DS", "F", "D", Gender: Gender.Male, ThirdPartyCompanyCode: closed.Code), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.UpdateEmployeeAsync(guard.Id, new EmployeeUpsertRequest("SEC_01", "Guard", null, "C", "DS", "F", "D", Gender: Gender.Male, ThirdPartyCompanyCode: avengers.Code), default));
        var movedOut = await service.UpdateEmployeeAsync(guard.Id, new EmployeeUpsertRequest("SEC_01", "Guard", null, "C", "DS", "F", "D", Gender: Gender.Male), default);
        Assert.Null(movedOut.ThirdPartyCompanyCode);

        await service.UpdateEmployeeAsync(guard.Id, new EmployeeUpsertRequest("SEC_01", "Guard", null, "TP", "DS", "F", "D", Gender: Gender.Male, ThirdPartyCompanyCode: avengers.Code), default);
        var inUse = await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DeactivateThirdPartyCompanyAsync(avengers.Id, default));
        Assert.Contains("active employees", inUse.Message);
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

        var otherFactory = await service.CreateEmployeeAsync(new EmployeeUpsertRequest(existing.EPF, "Other Person", null, "C", "DS", "F2", "D", Gender: Gender.Male), default);
        var duplicate = await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateEmployeeAsync(new EmployeeUpsertRequest(existing.EPF, "Same Factory", null, "C", "DS", "F", "D", Gender: Gender.Male), default));
        var moveIntoClash = await Assert.ThrowsAsync<MasterDataConflictException>(() => service.UpdateEmployeeAsync(otherFactory.Id, new EmployeeUpsertRequest(existing.EPF, "Other Person", null, "C", "DS", "F", "D", Gender: Gender.Male), default));

        Assert.Equal((existing.EPF, "F2"), (otherFactory.Epf, otherFactory.FactoryCode));
        Assert.Contains("selected factory", duplicate.Message);
        Assert.Contains("selected factory", moveIntoClash.Message);
    }

    [Fact]
    public async Task Allocations_follow_the_employee_record_not_the_epf_number()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context); var first = await SeedEmployeeAsync(context);
        context.Add(new Factory { Code = "F2", Name = "Factory Two" }); await context.SaveChangesAsync();
        var twin = await service.CreateEmployeeAsync(new EmployeeUpsertRequest(first.EPF, "Twin In Other Factory", null, "C", "DS", "F2", "D", Gender: Gender.Male), default);

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
