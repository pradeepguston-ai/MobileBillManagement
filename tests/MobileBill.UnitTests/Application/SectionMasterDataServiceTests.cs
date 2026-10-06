using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Application;

public sealed class SectionMasterDataServiceTests
{
    [Fact]
    public async Task Sections_belong_to_a_department_and_sub_sections_to_a_section()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await SeedReferencesAsync(context);

        var section = await service.CreateSectionAsync(new SectionUpsertRequest(" IT-INF ", "Infrastructure", "IT"), default);
        var subSection = await service.CreateSubSectionAsync(new SubSectionUpsertRequest("IT-NET", "Network", "IT-INF"), default);

        Assert.Equal(("IT-INF", "IT", "Information Technology"), (section.Code, section.DepartmentCode, section.DepartmentName));
        Assert.Equal(("IT-NET", "IT-INF", "Infrastructure", "IT", "Information Technology"), (subSection.Code, subSection.SectionCode, subSection.SectionName, subSection.DepartmentCode, subSection.DepartmentName));
    }

    [Fact]
    public async Task Sections_can_be_listed_for_one_department()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await SeedReferencesAsync(context);
        await service.CreateSectionAsync(new SectionUpsertRequest("IT-INF", "Infrastructure", "IT"), default);
        await service.CreateSectionAsync(new SectionUpsertRequest("HR-PAY", "Payroll", "HR"), default);

        var itSections = await service.GetSectionsAsync(new PagedRequest(), "IT", default);

        Assert.Equal(["IT-INF"], itSections.Items.Select(item => item.Code));
    }

    [Fact]
    public async Task Section_requires_an_active_department_and_a_unique_code()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await SeedReferencesAsync(context);
        await service.CreateSectionAsync(new SectionUpsertRequest("IT-INF", "Infrastructure", "IT"), default);

        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateSectionAsync(new SectionUpsertRequest("X", "X", "NOPE"), default));
        await Assert.ThrowsAsync<MasterDataConflictException>(() => service.CreateSectionAsync(new SectionUpsertRequest("IT-INF", "Again", "HR"), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateSubSectionAsync(new SubSectionUpsertRequest("X", "X", "NOPE"), default));
    }

    [Fact]
    public async Task Employee_section_must_match_the_department_and_sub_section_must_match_the_section()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await SeedReferencesAsync(context);
        await service.CreateSectionAsync(new SectionUpsertRequest("IT-INF", "Infrastructure", "IT"), default);
        await service.CreateSectionAsync(new SectionUpsertRequest("HR-PAY", "Payroll", "HR"), default);
        await service.CreateSubSectionAsync(new SubSectionUpsertRequest("IT-NET", "Network", "IT-INF"), default);
        await service.CreateSubSectionAsync(new SubSectionUpsertRequest("HR-SAL", "Salaries", "HR-PAY"), default);

        var employee = await service.CreateEmployeeAsync(Employee("E1", "IT", "IT-INF", "IT-NET"), default);
        Assert.Equal(("IT-INF", "Infrastructure", "IT-NET", "Network"), (employee.SectionCode, employee.SectionName, employee.SubSectionCode, employee.SubSectionName));

        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateEmployeeAsync(Employee("E2", "IT", "HR-PAY", null), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateEmployeeAsync(Employee("E3", "IT", "IT-INF", "HR-SAL"), default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.CreateEmployeeAsync(Employee("E4", "IT", null, "IT-NET"), default));
    }

    [Fact]
    public async Task Employee_without_a_section_is_still_valid()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await SeedReferencesAsync(context);

        var employee = await service.CreateEmployeeAsync(Employee("E1", "IT", " ", ""), default);

        Assert.Null(employee.SectionCode);
        Assert.Null(employee.SubSectionCode);
    }

    [Fact]
    public async Task Parents_cannot_be_deactivated_while_children_or_employees_use_them()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await SeedReferencesAsync(context);
        var section = await service.CreateSectionAsync(new SectionUpsertRequest("IT-INF", "Infrastructure", "IT"), default);
        var subSection = await service.CreateSubSectionAsync(new SubSectionUpsertRequest("IT-NET", "Network", "IT-INF"), default);
        await service.CreateEmployeeAsync(Employee("E1", "IT", "IT-INF", "IT-NET"), default);
        var itDepartment = await context.Departments.SingleAsync(item => item.Code == "IT");

        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DeactivateDepartmentAsync(itDepartment.Id, default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DeactivateSectionAsync(section.Id, default));
        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.DeactivateSubSectionAsync(subSection.Id, default));
    }

    [Fact]
    public async Task Section_in_use_cannot_move_to_another_department()
    {
        await using var context = CreateContext(); var service = new EfMasterDataService(context);
        await SeedReferencesAsync(context);
        var section = await service.CreateSectionAsync(new SectionUpsertRequest("IT-INF", "Infrastructure", "IT"), default);
        await service.CreateEmployeeAsync(Employee("E1", "IT", "IT-INF", null), default);

        await Assert.ThrowsAsync<MasterDataValidationException>(() => service.UpdateSectionAsync(section.Id, new SectionUpsertRequest("IT-INF", "Infrastructure", "HR"), default));
        var renamed = await service.UpdateSectionAsync(section.Id, new SectionUpsertRequest("IT-INF", "IT Infrastructure", "IT"), default);
        Assert.Equal("IT Infrastructure", renamed.Name);
    }

    private static EmployeeUpsertRequest Employee(string epf, string departmentCode, string? sectionCode, string? subSectionCode) =>
        new(epf, "Test Employee", null, "C", "DS", "F", departmentCode, sectionCode, subSectionCode);

    private static MobileBillDbContext CreateContext() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedReferencesAsync(MobileBillDbContext context)
    {
        context.AddRange(
            new Factory { Code = "F", Name = "Factory" },
            new Department { Code = "IT", Name = "Information Technology" },
            new Department { Code = "HR", Name = "Human Resources" },
            new EmployeeCategory { Code = "C", Name = "Category" },
            new Designation { Code = "DS", Name = "Designation" });
        await context.SaveChangesAsync();
    }
}
