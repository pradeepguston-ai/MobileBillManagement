using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;

namespace MobileBill.Infrastructure.MasterData;

// Department → Section → Sub Section. Codes are unique within each list, like the other masters.
public sealed partial class EfMasterDataService
{
    public async Task<PagedResult<SectionDto>> GetSectionsAsync(PagedRequest request, string? departmentCode, CancellationToken cancellationToken)
    {
        var query = _dbContext.Sections.AsNoTracking();
        if (request.IsActive is not null) query = query.Where(section => section.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(departmentCode)) { var department = departmentCode.Trim(); query = query.Where(section => section.DepartmentCode == department); }
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(section => section.Code.Contains(search) || section.Name.Contains(search) || section.Department.Name.Contains(search)); }
        return await ToPagedResultAsync(SectionProjection(query.OrderBy(section => section.DepartmentCode).ThenBy(section => section.Code)), request, cancellationToken);
    }

    public async Task<SectionDto> GetSectionAsync(Guid id, CancellationToken cancellationToken) =>
        await SectionProjection(_dbContext.Sections.AsNoTracking().Where(section => section.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw new MasterDataNotFoundException("Section", id);

    public async Task<SectionDto> CreateSectionAsync(SectionUpsertRequest request, CancellationToken cancellationToken)
    {
        var (code, name, departmentCode) = (request.Code?.Trim() ?? string.Empty, request.Name?.Trim() ?? string.Empty, request.DepartmentCode?.Trim() ?? string.Empty);
        ValidateReference(code, name); MasterDataValidation.RequireText(departmentCode, "Department");
        await RequireActiveDepartmentAsync(departmentCode, cancellationToken);
        if (await _dbContext.Sections.AnyAsync(section => section.Code == code, cancellationToken)) throw new MasterDataConflictException("Section code already exists.");
        var entity = new Section { Code = code, Name = name, DepartmentCode = departmentCode };
        _dbContext.Sections.Add(entity); await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetSectionAsync(entity.Id, cancellationToken);
    }

    public async Task<SectionDto> UpdateSectionAsync(Guid id, SectionUpsertRequest request, CancellationToken cancellationToken)
    {
        var (code, name, departmentCode) = (request.Code?.Trim() ?? string.Empty, request.Name?.Trim() ?? string.Empty, request.DepartmentCode?.Trim() ?? string.Empty);
        ValidateReference(code, name); MasterDataValidation.RequireText(departmentCode, "Department");
        var entity = await _dbContext.Sections.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Section", id);
        if (await _dbContext.Sections.AnyAsync(section => section.Id != id && section.Code == code, cancellationToken)) throw new MasterDataConflictException("Section code already exists.");
        if (entity.Code != code && await IsSectionReferencedAsync(entity.Code, cancellationToken))
            throw new MasterDataValidationException("Section code cannot be changed while employees or sub sections use it.");
        if (entity.DepartmentCode != departmentCode)
        {
            await RequireActiveDepartmentAsync(departmentCode, cancellationToken);
            if (await _dbContext.Employees.AnyAsync(employee => employee.SectionCode == entity.Code, cancellationToken))
                throw new MasterDataValidationException("Section cannot move to another department while employees are assigned to it.");
        }
        entity.Code = code; entity.Name = name; entity.DepartmentCode = departmentCode;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetSectionAsync(id, cancellationToken);
    }

    public async Task DeactivateSectionAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Sections.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Section", id);
        if (await _dbContext.Employees.AnyAsync(employee => employee.SectionCode == entity.Code && employee.IsActive, cancellationToken))
            throw new MasterDataValidationException("Section cannot be deactivated while active employees reference it.");
        if (await _dbContext.SubSections.AnyAsync(subSection => subSection.SectionCode == entity.Code && subSection.IsActive, cancellationToken))
            throw new MasterDataValidationException("Section cannot be deactivated while it has active sub sections.");
        entity.IsActive = false; await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<SubSectionDto>> GetSubSectionsAsync(PagedRequest request, string? sectionCode, CancellationToken cancellationToken)
    {
        var query = _dbContext.SubSections.AsNoTracking();
        if (request.IsActive is not null) query = query.Where(subSection => subSection.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(sectionCode)) { var section = sectionCode.Trim(); query = query.Where(subSection => subSection.SectionCode == section); }
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(subSection => subSection.Code.Contains(search) || subSection.Name.Contains(search) || subSection.Section.Name.Contains(search)); }
        return await ToPagedResultAsync(SubSectionProjection(query.OrderBy(subSection => subSection.SectionCode).ThenBy(subSection => subSection.Code)), request, cancellationToken);
    }

    public async Task<SubSectionDto> GetSubSectionAsync(Guid id, CancellationToken cancellationToken) =>
        await SubSectionProjection(_dbContext.SubSections.AsNoTracking().Where(subSection => subSection.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw new MasterDataNotFoundException("Sub section", id);

    public async Task<SubSectionDto> CreateSubSectionAsync(SubSectionUpsertRequest request, CancellationToken cancellationToken)
    {
        var (code, name, sectionCode) = (request.Code?.Trim() ?? string.Empty, request.Name?.Trim() ?? string.Empty, request.SectionCode?.Trim() ?? string.Empty);
        ValidateReference(code, name); MasterDataValidation.RequireText(sectionCode, "Section");
        await RequireActiveSectionAsync(sectionCode, cancellationToken);
        if (await _dbContext.SubSections.AnyAsync(subSection => subSection.Code == code, cancellationToken)) throw new MasterDataConflictException("Sub section code already exists.");
        var entity = new SubSection { Code = code, Name = name, SectionCode = sectionCode };
        _dbContext.SubSections.Add(entity); await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetSubSectionAsync(entity.Id, cancellationToken);
    }

    public async Task<SubSectionDto> UpdateSubSectionAsync(Guid id, SubSectionUpsertRequest request, CancellationToken cancellationToken)
    {
        var (code, name, sectionCode) = (request.Code?.Trim() ?? string.Empty, request.Name?.Trim() ?? string.Empty, request.SectionCode?.Trim() ?? string.Empty);
        ValidateReference(code, name); MasterDataValidation.RequireText(sectionCode, "Section");
        var entity = await _dbContext.SubSections.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Sub section", id);
        if (await _dbContext.SubSections.AnyAsync(subSection => subSection.Id != id && subSection.Code == code, cancellationToken)) throw new MasterDataConflictException("Sub section code already exists.");
        var inUse = await _dbContext.Employees.AnyAsync(employee => employee.SubSectionCode == entity.Code, cancellationToken);
        if (entity.Code != code && inUse) throw new MasterDataValidationException("Sub section code cannot be changed while employees use it.");
        if (entity.SectionCode != sectionCode)
        {
            await RequireActiveSectionAsync(sectionCode, cancellationToken);
            if (inUse) throw new MasterDataValidationException("Sub section cannot move to another section while employees are assigned to it.");
        }
        entity.Code = code; entity.Name = name; entity.SectionCode = sectionCode;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetSubSectionAsync(id, cancellationToken);
    }

    public async Task DeactivateSubSectionAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SubSections.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Sub section", id);
        if (await _dbContext.Employees.AnyAsync(employee => employee.SubSectionCode == entity.Code && employee.IsActive, cancellationToken))
            throw new MasterDataValidationException("Sub section cannot be deactivated while active employees reference it.");
        entity.IsActive = false; await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireActiveDepartmentAsync(string departmentCode, CancellationToken token)
    {
        if (!await _dbContext.Departments.AnyAsync(department => department.Code == departmentCode && department.IsActive, token))
            throw new MasterDataValidationException("An active department is required.");
    }

    private async Task RequireActiveSectionAsync(string sectionCode, CancellationToken token)
    {
        if (!await _dbContext.Sections.AnyAsync(section => section.Code == sectionCode && section.IsActive, token))
            throw new MasterDataValidationException("An active section is required.");
    }

    private async Task<bool> IsSectionReferencedAsync(string sectionCode, CancellationToken token) =>
        await _dbContext.Employees.AnyAsync(employee => employee.SectionCode == sectionCode, token)
        || await _dbContext.SubSections.AnyAsync(subSection => subSection.SectionCode == sectionCode, token);

    // An employee's Section must belong to their Department, and their Sub Section to that Section.
    private async Task<(string? SectionCode, string? SubSectionCode)> ResolveEmployeeSectionsAsync(EmployeeUpsertRequest request, CancellationToken token)
    {
        var sectionCode = TrimOptional(request.SectionCode);
        var subSectionCode = TrimOptional(request.SubSectionCode);
        if (sectionCode is null)
        {
            if (subSectionCode is not null) throw new MasterDataValidationException("Select a section before choosing a sub section.");
            return (null, null);
        }
        var departmentCode = request.DepartmentCode.Trim();
        if (!await _dbContext.Sections.AnyAsync(section => section.Code == sectionCode && section.IsActive && section.DepartmentCode == departmentCode, token))
            throw new MasterDataValidationException("The section must be an active section of the employee's department.");
        if (subSectionCode is not null && !await _dbContext.SubSections.AnyAsync(subSection => subSection.Code == subSectionCode && subSection.IsActive && subSection.SectionCode == sectionCode, token))
            throw new MasterDataValidationException("The sub section must be an active sub section of the selected section.");
        return (sectionCode, subSectionCode);
    }

    private static IQueryable<SectionDto> SectionProjection(IQueryable<Section> query) =>
        query.Select(section => new SectionDto(section.Id, section.Code, section.Name, section.DepartmentCode, section.Department.Name, section.IsActive));

    private static IQueryable<SubSectionDto> SubSectionProjection(IQueryable<SubSection> query) =>
        query.Select(subSection => new SubSectionDto(subSection.Id, subSection.Code, subSection.Name, subSection.SectionCode, subSection.Section.Name, subSection.Section.DepartmentCode, subSection.Section.Department.Name, subSection.IsActive));
}
