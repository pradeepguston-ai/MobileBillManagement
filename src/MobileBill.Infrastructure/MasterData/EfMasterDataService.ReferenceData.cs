using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;

namespace MobileBill.Infrastructure.MasterData;

public sealed partial class EfMasterDataService
{
    public Task<PagedResult<ReferenceDataDto>> GetFactoriesAsync(PagedRequest request, CancellationToken cancellationToken) =>
        GetReferencesAsync(_dbContext.Factories, request, factory => new ReferenceDataDto(factory.Id, factory.Code, factory.Name, factory.IsActive), factory => factory.Code, factory => factory.Name, factory => factory.IsActive, cancellationToken);
    public Task<ReferenceDataDto> GetFactoryAsync(Guid id, CancellationToken cancellationToken) => GetReferenceAsync(_dbContext.Factories, id, "Factory", factory => new ReferenceDataDto(factory.Id, factory.Code, factory.Name, factory.IsActive), cancellationToken);
    public Task<ReferenceDataDto> CreateFactoryAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => CreateReferenceAsync(_dbContext.Factories, request, () => new Factory { Code = string.Empty, Name = string.Empty }, "Factory", cancellationToken);
    public Task<ReferenceDataDto> UpdateFactoryAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => UpdateReferenceAsync(_dbContext.Factories, id, request, "Factory", cancellationToken);
    public Task DeactivateFactoryAsync(Guid id, CancellationToken cancellationToken) => DeactivateReferenceAsync(_dbContext.Factories, id, "Factory", code => _dbContext.Employees.AnyAsync(employee => employee.FactoryCode == code && employee.IsActive, cancellationToken), cancellationToken);

    public Task<PagedResult<ReferenceDataDto>> GetDesignationsAsync(PagedRequest request, CancellationToken cancellationToken) =>
        GetReferencesAsync(_dbContext.Designations, request, designation => new ReferenceDataDto(designation.Id, designation.Code, designation.Name, designation.IsActive), designation => designation.Code, designation => designation.Name, designation => designation.IsActive, cancellationToken);
    public Task<ReferenceDataDto> GetDesignationAsync(Guid id, CancellationToken cancellationToken) => GetReferenceAsync(_dbContext.Designations, id, "Designation", designation => new ReferenceDataDto(designation.Id, designation.Code, designation.Name, designation.IsActive), cancellationToken);
    public Task<ReferenceDataDto> CreateDesignationAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => CreateReferenceAsync(_dbContext.Designations, request, () => new Designation { Code = string.Empty, Name = string.Empty }, "Designation", cancellationToken);
    public Task<ReferenceDataDto> UpdateDesignationAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => UpdateReferenceAsync(_dbContext.Designations, id, request, "Designation", cancellationToken);
    public Task DeactivateDesignationAsync(Guid id, CancellationToken cancellationToken) => DeactivateReferenceAsync(_dbContext.Designations, id, "Designation", code => _dbContext.Employees.AnyAsync(employee => employee.DesignationCode == code && employee.IsActive, cancellationToken), cancellationToken);

    public Task<PagedResult<ReferenceDataDto>> GetCategoriesAsync(PagedRequest request, CancellationToken cancellationToken) =>
        GetReferencesAsync(_dbContext.EmployeeCategories, request, category => new ReferenceDataDto(category.Id, category.Code, category.Name, category.IsActive), category => category.Code, category => category.Name, category => category.IsActive, cancellationToken);
    public Task<ReferenceDataDto> GetCategoryAsync(Guid id, CancellationToken cancellationToken) => GetReferenceAsync(_dbContext.EmployeeCategories, id, "Category", category => new ReferenceDataDto(category.Id, category.Code, category.Name, category.IsActive), cancellationToken);
    public Task<ReferenceDataDto> CreateCategoryAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => CreateReferenceAsync(_dbContext.EmployeeCategories, request, () => new EmployeeCategory { Code = string.Empty, Name = string.Empty }, "Category", cancellationToken);
    public Task<ReferenceDataDto> UpdateCategoryAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => UpdateReferenceAsync(_dbContext.EmployeeCategories, id, request, "Category", cancellationToken);
    public Task DeactivateCategoryAsync(Guid id, CancellationToken cancellationToken) => DeactivateReferenceAsync(_dbContext.EmployeeCategories, id, "Category", code => _dbContext.Employees.AnyAsync(employee => employee.CategoryCode == code && employee.IsActive, cancellationToken), cancellationToken);

    public Task<PagedResult<ReferenceDataDto>> GetProvidersAsync(PagedRequest request, CancellationToken cancellationToken) =>
        GetReferencesAsync(_dbContext.TelecomProviders, request, provider => new ReferenceDataDto(provider.Id, provider.Code, provider.Name, provider.IsActive), provider => provider.Code, provider => provider.Name, provider => provider.IsActive, cancellationToken);
    public Task<ReferenceDataDto> GetProviderAsync(Guid id, CancellationToken cancellationToken) => GetReferenceAsync(_dbContext.TelecomProviders, id, "Provider", provider => new ReferenceDataDto(provider.Id, provider.Code, provider.Name, provider.IsActive), cancellationToken);
    public Task<ReferenceDataDto> CreateProviderAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => CreateReferenceAsync(_dbContext.TelecomProviders, request, () => new TelecomProvider { Code = string.Empty, Name = string.Empty }, "Provider", cancellationToken);
    public Task<ReferenceDataDto> UpdateProviderAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => UpdateReferenceAsync(_dbContext.TelecomProviders, id, request, "Provider", cancellationToken);
    public Task DeactivateProviderAsync(Guid id, CancellationToken cancellationToken) => DeactivateReferenceAsync(_dbContext.TelecomProviders, id, "Provider", _ => Task.FromResult(false), cancellationToken);

    public Task<PagedResult<ReferenceDataDto>> GetDepartmentsAsync(PagedRequest request, CancellationToken cancellationToken) =>
        GetReferencesAsync(_dbContext.Departments, request, department => new ReferenceDataDto(department.Id, department.Code, department.Name, department.IsActive), department => department.Code, department => department.Name, department => department.IsActive, cancellationToken);
    public Task<ReferenceDataDto> GetDepartmentAsync(Guid id, CancellationToken cancellationToken) => GetReferenceAsync(_dbContext.Departments, id, "Department", department => new ReferenceDataDto(department.Id, department.Code, department.Name, department.IsActive), cancellationToken);
    public Task<ReferenceDataDto> CreateDepartmentAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => CreateReferenceAsync(_dbContext.Departments, request, () => new Department { Code = string.Empty, Name = string.Empty }, "Department", cancellationToken);
    public Task<ReferenceDataDto> UpdateDepartmentAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken) => UpdateReferenceAsync(_dbContext.Departments, id, request, "Department", cancellationToken);
    public async Task DeactivateDepartmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw new MasterDataNotFoundException("Department", id);
        if (await _dbContext.Sections.AnyAsync(section => section.DepartmentCode == department.Code && section.IsActive, cancellationToken))
            throw new MasterDataValidationException("Department cannot be deactivated while it has active sections.");
        await DeactivateReferenceAsync(_dbContext.Departments, id, "Department", code => _dbContext.Employees.AnyAsync(employee => employee.DepartmentCode == code && employee.IsActive, cancellationToken), cancellationToken);
    }

    private static void ValidateReference(string code, string name) { MasterDataValidation.RequireText(code, "Code"); MasterDataValidation.RequireText(name, "Name"); }

    private async Task<PagedResult<ReferenceDataDto>> GetReferencesAsync<TEntity>(DbSet<TEntity> set, PagedRequest request, Expression<Func<TEntity, ReferenceDataDto>> projection, Expression<Func<TEntity, string>> code, Expression<Func<TEntity, string>> name, Expression<Func<TEntity, bool>> active, CancellationToken token) where TEntity : class
    {
        var query = set.AsNoTracking().AsQueryable();
        query = ApplyStatusFilter(query, request.IsActive, active);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(entity => EF.Property<string>(entity, "Code").Contains(search) || EF.Property<string>(entity, "Name").Contains(search)); }
        return await ToPagedResultAsync(query.OrderBy(code).Select(projection), request, token);
    }
    private async Task<ReferenceDataDto> GetReferenceAsync<TEntity>(DbSet<TEntity> set, Guid id, string resource, Expression<Func<TEntity, ReferenceDataDto>> projection, CancellationToken token) where TEntity : class => await set.AsNoTracking().Where(entity => EF.Property<Guid>(entity, "Id") == id).Select(projection).SingleOrDefaultAsync(token) ?? throw new MasterDataNotFoundException(resource, id);
    private async Task<ReferenceDataDto> CreateReferenceAsync<TEntity>(DbSet<TEntity> set, ReferenceDataUpsertRequest request, Func<TEntity> create, string resource, CancellationToken token) where TEntity : class
    {
        ValidateReference(request.Code, request.Name);
        if (await set.AnyAsync(entity => EF.Property<string>(entity, "Code") == request.Code.Trim(), token)) throw new MasterDataConflictException($"{resource} code already exists.");
        var entity = create();
        _dbContext.Entry(entity).Property<string>("Code").CurrentValue = request.Code.Trim();
        _dbContext.Entry(entity).Property<string>("Name").CurrentValue = request.Name.Trim();
        set.Add(entity); await _dbContext.SaveChangesAsync(token);
        return new ReferenceDataDto(_dbContext.Entry(entity).Property<Guid>("Id").CurrentValue, request.Code.Trim(), request.Name.Trim(), true);
    }
    private async Task<ReferenceDataDto> UpdateReferenceAsync<TEntity>(DbSet<TEntity> set, Guid id, ReferenceDataUpsertRequest request, string resource, CancellationToken token) where TEntity : class
    {
        ValidateReference(request.Code, request.Name); var entity = await set.FindAsync([id], token) ?? throw new MasterDataNotFoundException(resource, id);
        if (await set.AnyAsync(other => EF.Property<Guid>(other, "Id") != id && EF.Property<string>(other, "Code") == request.Code.Trim(), token)) throw new MasterDataConflictException($"{resource} code already exists.");
        _dbContext.Entry(entity).Property<string>("Code").CurrentValue = request.Code.Trim();
        _dbContext.Entry(entity).Property<string>("Name").CurrentValue = request.Name.Trim();
        await _dbContext.SaveChangesAsync(token);
        return new ReferenceDataDto(id, request.Code.Trim(), request.Name.Trim(), _dbContext.Entry(entity).Property<bool>("IsActive").CurrentValue);
    }
    private async Task DeactivateReferenceAsync<TEntity>(DbSet<TEntity> set, Guid id, string resource, Func<string, Task<bool>> inUseByCode, CancellationToken token) where TEntity : class
    {
        var entity = await set.FindAsync([id], token) ?? throw new MasterDataNotFoundException(resource, id);
        var code = _dbContext.Entry(entity).Property<string>("Code").CurrentValue;
        if (await inUseByCode(code)) throw new MasterDataValidationException($"{resource} cannot be deactivated while active employees reference it.");
        _dbContext.Entry(entity).Property<bool>("IsActive").CurrentValue = false; await _dbContext.SaveChangesAsync(token);
    }
}
