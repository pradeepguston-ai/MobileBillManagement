using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;

namespace MobileBill.Infrastructure.MasterData;

public sealed partial class EfMasterDataService
{
    public async Task<PagedResult<EmployeeDto>> GetEmployeesAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Employees.AsNoTracking();
        if (request.IsActive is not null) query = query.Where(employee => employee.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(employee => employee.EPF.Contains(search) || employee.FullName.Contains(search) || (employee.CallingName != null && employee.CallingName.Contains(search))); }
        return await ToPagedResultAsync(EmployeeProjection(query.OrderBy(employee => employee.EPF).ThenBy(employee => employee.FactoryCode)), request, cancellationToken);
    }
    public async Task<EmployeeDto> GetEmployeeAsync(Guid id, CancellationToken cancellationToken) => await EmployeeProjection(_dbContext.Employees.AsNoTracking().Where(employee => employee.Id == id)).SingleOrDefaultAsync(cancellationToken) ?? throw new MasterDataNotFoundException("Employee", id);
    public async Task<EmployeeDto> CreateEmployeeAsync(EmployeeUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateEmployee(request); await RequireEmployeeReferencesAsync(request, cancellationToken);
        var (sectionCode, subSectionCode) = await ResolveEmployeeSectionsAsync(request, cancellationToken);
        var epf = request.Epf.Trim(); var factoryCode = request.FactoryCode.Trim(); if (await _dbContext.Employees.AnyAsync(employee => employee.EPF == epf && employee.FactoryCode == factoryCode, cancellationToken)) throw new MasterDataConflictException(DuplicateEpfMessage);
        var entity = new Employee { EPF = epf, FullName = request.FullName.Trim(), CallingName = TrimOptional(request.CallingName), CategoryCode = request.CategoryCode.Trim(), DesignationCode = request.DesignationCode.Trim(), FactoryCode = request.FactoryCode.Trim(), DepartmentCode = request.DepartmentCode.Trim(), SectionCode = sectionCode, SubSectionCode = subSectionCode };
        _dbContext.Employees.Add(entity); await _dbContext.SaveChangesAsync(cancellationToken); return await GetEmployeeAsync(entity.Id, cancellationToken);
    }
    public async Task<EmployeeDto> UpdateEmployeeAsync(Guid id, EmployeeUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateEmployee(request); await RequireEmployeeReferencesAsync(request, cancellationToken);
        var (sectionCode, subSectionCode) = await ResolveEmployeeSectionsAsync(request, cancellationToken);
        var entity = await _dbContext.Employees.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Employee", id);
        var epf = request.Epf.Trim(); var factoryCode = request.FactoryCode.Trim(); if (await _dbContext.Employees.AnyAsync(employee => employee.Id != id && employee.EPF == epf && employee.FactoryCode == factoryCode, cancellationToken)) throw new MasterDataConflictException(DuplicateEpfMessage);
        entity.EPF = epf; entity.FullName = request.FullName.Trim(); entity.CallingName = TrimOptional(request.CallingName); entity.CategoryCode = request.CategoryCode.Trim(); entity.DesignationCode = request.DesignationCode.Trim(); entity.FactoryCode = request.FactoryCode.Trim(); entity.DepartmentCode = request.DepartmentCode.Trim(); entity.SectionCode = sectionCode; entity.SubSectionCode = subSectionCode;
        await _dbContext.SaveChangesAsync(cancellationToken); return await GetEmployeeAsync(id, cancellationToken);
    }
    public async Task DeactivateEmployeeAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Employees.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Employee", id);
        // Numbers already in the SIM Pool no longer belong to the employee, so only assigned numbers block this.
        if (await _dbContext.MobileAccounts.AnyAsync(account => account.EmployeeId == entity.Id && account.IsActive && account.Status == SimStatus.Assigned, cancellationToken)) throw new MasterDataValidationException("Employee still holds mobile numbers. Use Resign to release them to the SIM Pool, or release each number to the pool first.");
        entity.IsActive = false; await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<MobileAccountDto>> GetMobileAccountsAsync(PagedRequest request, CancellationToken cancellationToken, SimStatus? status = null)
    {
        var query = _dbContext.MobileAccounts.AsNoTracking();
        if (request.IsActive is not null) query = query.Where(account => account.IsActive == request.IsActive.Value);
        if (status is not null) query = query.Where(account => account.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(account => account.MobileNumber.Contains(search) || account.Employee.EPF.Contains(search) || account.Employee.FullName.Contains(search) || (account.Package != null && account.Package.Code.Contains(search))); }
        return await ToPagedResultAsync(MobileAccountProjection(query.OrderBy(account => account.MobileNumber).ThenBy(account => account.Id)), request, cancellationToken);
    }
    public async Task<MobileAccountDto> GetMobileAccountAsync(Guid id, CancellationToken cancellationToken) => await MobileAccountProjection(_dbContext.MobileAccounts.AsNoTracking().Where(account => account.Id == id)).SingleOrDefaultAsync(cancellationToken) ?? throw new MasterDataNotFoundException("Mobile account", id);
    public async Task<MobileAccountDto> CreateMobileAccountAsync(MobileAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateMobileAccount(request);
        await RequireActiveEmployeeAsync(request.EmployeeId, cancellationToken);
        await EnsureNoActiveMobileAccountAsync(request.MobileNumber, null, cancellationToken);
        if (request.PackageId is null || request.PackageId == Guid.Empty) throw new MasterDataValidationException("Select the package.");
        var entity = new MobileAccount
        {
            MobileNumber = request.MobileNumber.Trim(),
            EmployeeId = request.EmployeeId,
            MonthlyCreditLimit = request.MonthlyCreditLimit,
            MonthlyRental = request.MonthlyRental,
            SimType = request.SimType,
            PackageId = await ResolveAllocationPackageAsync(request.PackageId, null, cancellationToken)
        };
        _dbContext.MobileAccounts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetMobileAccountAsync(entity.Id, cancellationToken);
    }

    public async Task<MobileAccountDto> UpdateMobileAccountAsync(Guid id, MobileAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateMobileAccount(request);
        await RequireActiveEmployeeAsync(request.EmployeeId, cancellationToken);
        var entity = await _dbContext.MobileAccounts.FindAsync([id], cancellationToken)
            ?? throw new MasterDataNotFoundException("Mobile account", id);
        if (entity.IsActive)
            await EnsureNoActiveMobileAccountAsync(request.MobileNumber, id, cancellationToken);
        entity.MobileNumber = request.MobileNumber.Trim();
        entity.EmployeeId = request.EmployeeId;
        entity.MonthlyCreditLimit = request.MonthlyCreditLimit;
        entity.MonthlyRental = request.MonthlyRental;
        entity.SimType = request.SimType;
        entity.PackageId = await ResolveAllocationPackageAsync(request.PackageId, entity.PackageId, cancellationToken);
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
    public async Task<MobileAccountDto> ReassignMobileAccountAsync(Guid id, MobileAccountReassignRequest request, CancellationToken cancellationToken)
    {
        RequireEmployeeSelected(request.EmployeeId);
        var entity = await _dbContext.MobileAccounts.FindAsync([id], cancellationToken)
            ?? throw new MasterDataNotFoundException("Mobile account", id);
        if (!entity.IsActive) throw new MasterDataValidationException("Only an active mobile allocation can be reassigned.");
        if (entity.Status != SimStatus.Assigned) throw new MasterDataValidationException("This number is in the SIM Pool. Use Assign from Pool instead.");
        await RequireActiveEmployeeAsync(request.EmployeeId, cancellationToken);
        if (entity.EmployeeId == request.EmployeeId) throw new MasterDataValidationException("The mobile number is already assigned to this employee.");
        entity.EmployeeId = request.EmployeeId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetMobileAccountAsync(id, cancellationToken);
    }

    private static IQueryable<EmployeeDto> EmployeeProjection(IQueryable<Employee> query) => query.Select(employee => new EmployeeDto(employee.Id, employee.EPF, employee.FullName, employee.CallingName, employee.CategoryCode, employee.Category.Name, employee.DesignationCode, employee.Designation.Name, employee.FactoryCode, employee.Factory.Name, employee.DepartmentCode, employee.Department.Name, employee.IsActive, employee.SectionCode, employee.Section != null ? employee.Section.Name : null, employee.SubSectionCode, employee.SubSection != null ? employee.SubSection.Name : null, employee.ResignedOn));
    private static IQueryable<MobileAccountDto> MobileAccountProjection(IQueryable<MobileAccount> query) => query.Select(account => new MobileAccountDto(account.Id, account.MobileNumber, account.Employee.EPF, account.Employee.FullName, account.Employee.Factory.Name, account.Employee.Department.Name, account.MonthlyCreditLimit, account.MonthlyRental, account.IsActive, account.Status, account.PooledOn, account.DisconnectedOn, account.StatusReason, account.EmployeeId, account.Employee.FactoryCode,
        account.PackageId, account.Package != null ? account.Package.Code : null, account.Package != null && account.MonthlyRental != account.Package.MonthlyRental, account.SimType));
    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void ValidateEmployee(EmployeeUpsertRequest request) { MasterDataValidation.RequireText(request.Epf, "EPF"); MasterDataValidation.RequireText(request.FullName, "Full Name"); }
    private static void ValidateMobileAccount(MobileAccountUpsertRequest request) { MasterDataValidation.RequireText(request.MobileNumber, "Mobile Number"); MasterDataValidation.RequireCurrencyAmount(request.MonthlyCreditLimit, "Monthly Credit Limit"); MasterDataValidation.RequireCurrencyAmount(request.MonthlyRental, "Monthly Rental"); }
    private async Task RequireEmployeeReferencesAsync(EmployeeUpsertRequest request, CancellationToken token)
    {
        if (!await _dbContext.EmployeeCategories.AnyAsync(category => category.Code == request.CategoryCode && category.IsActive, token) || !await _dbContext.Designations.AnyAsync(designation => designation.Code == request.DesignationCode && designation.IsActive, token) || !await _dbContext.Factories.AnyAsync(factory => factory.Code == request.FactoryCode && factory.IsActive, token) || !await _dbContext.Departments.AnyAsync(department => department.Code == request.DepartmentCode && department.IsActive, token)) throw new MasterDataValidationException("Employee must reference active category, designation, factory, and department records.");
    }
    private const string DuplicateEpfMessage = "This EPF already exists in the selected factory.";
    private static void RequireEmployeeSelected(Guid employeeId) { if (employeeId == Guid.Empty) throw new MasterDataValidationException("Select the employee."); }
    private async Task RequireActiveEmployeeAsync(Guid employeeId, CancellationToken token) { if (employeeId == Guid.Empty || !await _dbContext.Employees.AnyAsync(employee => employee.Id == employeeId && employee.IsActive, token)) throw new MasterDataValidationException("An active employee is required."); }
    private async Task EnsureNoActiveMobileAccountAsync(string mobileNumber, Guid? excludedId, CancellationToken token)
    {
        var number = mobileNumber.Trim();
        var existing = await _dbContext.MobileAccounts.AsNoTracking().Where(account => account.IsActive && account.MobileNumber == number && (!excludedId.HasValue || account.Id != excludedId.Value)).Select(account => (SimStatus?)account.Status).FirstOrDefaultAsync(token);
        if (existing == SimStatus.Pooled) throw new MasterDataConflictException("Mobile number is in the SIM Pool. Use Assign from Pool to give it to the new holder.");
        if (existing is not null) throw new MasterDataConflictException("Mobile number already has an active allocation.");
    }
}
