using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;

namespace MobileBill.Infrastructure.MasterData;

public sealed partial class EfMasterDataService
{
    public async Task<PagedResult<EmployeeDto>> GetEmployeesAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Employees.AsNoTracking();
        if (request.IsActive is not null) query = query.Where(employee => employee.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(employee => employee.EPF.Contains(search) || employee.FullName.Contains(search) || (employee.CallingName != null && employee.CallingName.Contains(search))); }
        return await ToPagedResultAsync(EmployeeProjection(query.OrderBy(employee => employee.EPF)), request, cancellationToken);
    }
    public async Task<EmployeeDto> GetEmployeeAsync(Guid id, CancellationToken cancellationToken) => await EmployeeProjection(_dbContext.Employees.AsNoTracking().Where(employee => employee.Id == id)).SingleOrDefaultAsync(cancellationToken) ?? throw new MasterDataNotFoundException("Employee", id);
    public async Task<EmployeeDto> CreateEmployeeAsync(EmployeeUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateEmployee(request); await RequireEmployeeReferencesAsync(request, cancellationToken);
        var epf = request.Epf.Trim(); if (await _dbContext.Employees.AnyAsync(employee => employee.EPF == epf, cancellationToken)) throw new MasterDataConflictException("EPF already exists.");
        var entity = new Employee { EPF = epf, FullName = request.FullName.Trim(), CallingName = TrimOptional(request.CallingName), CategoryCode = request.CategoryCode.Trim(), DesignationCode = request.DesignationCode.Trim(), FactoryCode = request.FactoryCode.Trim(), DepartmentCode = request.DepartmentCode.Trim() };
        _dbContext.Employees.Add(entity); await _dbContext.SaveChangesAsync(cancellationToken); return await GetEmployeeAsync(entity.Id, cancellationToken);
    }
    public async Task<EmployeeDto> UpdateEmployeeAsync(Guid id, EmployeeUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateEmployee(request); await RequireEmployeeReferencesAsync(request, cancellationToken);
        var entity = await _dbContext.Employees.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Employee", id);
        var epf = request.Epf.Trim(); if (await _dbContext.Employees.AnyAsync(employee => employee.Id != id && employee.EPF == epf, cancellationToken)) throw new MasterDataConflictException("EPF already exists.");
        entity.EPF = epf; entity.FullName = request.FullName.Trim(); entity.CallingName = TrimOptional(request.CallingName); entity.CategoryCode = request.CategoryCode.Trim(); entity.DesignationCode = request.DesignationCode.Trim(); entity.FactoryCode = request.FactoryCode.Trim(); entity.DepartmentCode = request.DepartmentCode.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken); return await GetEmployeeAsync(id, cancellationToken);
    }
    public async Task DeactivateEmployeeAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Employees.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Employee", id);
        if (await _dbContext.MobileAccounts.AnyAsync(account => account.EmployeeEpf == entity.EPF && account.IsActive, cancellationToken)) throw new MasterDataValidationException("Employee cannot be deactivated while active mobile account allocations exist.");
        entity.IsActive = false; await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<MobileAccountDto>> GetMobileAccountsAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var query = _dbContext.MobileAccounts.AsNoTracking();
        if (request.IsActive is not null) query = query.Where(account => account.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(account => account.MobileNumber.Contains(search) || account.Employee.EPF.Contains(search) || account.Employee.FullName.Contains(search)); }
        return await ToPagedResultAsync(MobileAccountProjection(query.OrderBy(account => account.MobileNumber).ThenBy(account => account.Id)), request, cancellationToken);
    }
    public async Task<MobileAccountDto> GetMobileAccountAsync(Guid id, CancellationToken cancellationToken) => await MobileAccountProjection(_dbContext.MobileAccounts.AsNoTracking().Where(account => account.Id == id)).SingleOrDefaultAsync(cancellationToken) ?? throw new MasterDataNotFoundException("Mobile account", id);
    public async Task<MobileAccountDto> CreateMobileAccountAsync(MobileAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateMobileAccount(request);
        await RequireActiveEmployeeAsync(request.EmployeeEpf, cancellationToken);
        await EnsureNoActiveMobileAccountAsync(request.MobileNumber, null, cancellationToken);
        var entity = new MobileAccount
        {
            MobileNumber = request.MobileNumber.Trim(),
            EmployeeEpf = request.EmployeeEpf.Trim(),
            MonthlyCreditLimit = request.MonthlyCreditLimit,
            MonthlyRental = request.MonthlyRental
        };
        _dbContext.MobileAccounts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetMobileAccountAsync(entity.Id, cancellationToken);
    }

    public async Task<MobileAccountDto> UpdateMobileAccountAsync(Guid id, MobileAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateMobileAccount(request);
        await RequireActiveEmployeeAsync(request.EmployeeEpf, cancellationToken);
        var entity = await _dbContext.MobileAccounts.FindAsync([id], cancellationToken)
            ?? throw new MasterDataNotFoundException("Mobile account", id);
        if (entity.IsActive)
            await EnsureNoActiveMobileAccountAsync(request.MobileNumber, id, cancellationToken);
        entity.MobileNumber = request.MobileNumber.Trim();
        entity.EmployeeEpf = request.EmployeeEpf.Trim();
        entity.MonthlyCreditLimit = request.MonthlyCreditLimit;
        entity.MonthlyRental = request.MonthlyRental;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetMobileAccountAsync(id, cancellationToken);
    }

    public async Task DeactivateMobileAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.MobileAccounts.FindAsync([id], cancellationToken)
            ?? throw new MasterDataNotFoundException("Mobile account", id);
        entity.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    private static IQueryable<EmployeeDto> EmployeeProjection(IQueryable<Employee> query) => query.Select(employee => new EmployeeDto(employee.Id, employee.EPF, employee.FullName, employee.CallingName, employee.CategoryCode, employee.Category.Name, employee.DesignationCode, employee.Designation.Name, employee.FactoryCode, employee.Factory.Name, employee.DepartmentCode, employee.Department.Name, employee.IsActive));
    private static IQueryable<MobileAccountDto> MobileAccountProjection(IQueryable<MobileAccount> query) => query.Select(account => new MobileAccountDto(account.Id, account.MobileNumber, account.EmployeeEpf, account.Employee.FullName, account.Employee.Factory.Name, account.Employee.Department.Name, account.MonthlyCreditLimit, account.MonthlyRental, account.IsActive));
    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void ValidateEmployee(EmployeeUpsertRequest request) { MasterDataValidation.RequireText(request.Epf, "EPF"); MasterDataValidation.RequireText(request.FullName, "Full Name"); }
    private static void ValidateMobileAccount(MobileAccountUpsertRequest request) { MasterDataValidation.RequireText(request.MobileNumber, "Mobile Number"); MasterDataValidation.RequireCurrencyAmount(request.MonthlyCreditLimit, "Monthly Credit Limit"); MasterDataValidation.RequireCurrencyAmount(request.MonthlyRental, "Monthly Rental"); }
    private async Task RequireEmployeeReferencesAsync(EmployeeUpsertRequest request, CancellationToken token)
    {
        if (!await _dbContext.EmployeeCategories.AnyAsync(category => category.Code == request.CategoryCode && category.IsActive, token) || !await _dbContext.Designations.AnyAsync(designation => designation.Code == request.DesignationCode && designation.IsActive, token) || !await _dbContext.Factories.AnyAsync(factory => factory.Code == request.FactoryCode && factory.IsActive, token) || !await _dbContext.Departments.AnyAsync(department => department.Code == request.DepartmentCode && department.IsActive, token)) throw new MasterDataValidationException("Employee must reference active category, designation, factory, and department records.");
    }
    private async Task RequireActiveEmployeeAsync(string epf, CancellationToken token) { if (!await _dbContext.Employees.AnyAsync(employee => employee.EPF == epf && employee.IsActive, token)) throw new MasterDataValidationException("An active employee is required."); }
    private async Task EnsureNoActiveMobileAccountAsync(string mobileNumber, Guid? excludedId, CancellationToken token)
    {
        var number = mobileNumber.Trim();
        if (await _dbContext.MobileAccounts.AnyAsync(account => account.IsActive && account.MobileNumber == number && (!excludedId.HasValue || account.Id != excludedId.Value), token)) throw new MasterDataConflictException("Mobile number already has an active allocation.");
    }
}
