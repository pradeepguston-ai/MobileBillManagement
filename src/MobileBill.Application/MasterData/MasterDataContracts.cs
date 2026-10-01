using MobileBill.Application.Common;

namespace MobileBill.Application.MasterData;

public sealed record ReferenceDataDto(Guid Id, string Code, string Name, bool IsActive);
public sealed record ReferenceDataUpsertRequest(string Code, string Name);

public sealed record EmployeeDto(
    Guid Id, string Epf, string FullName, string? CallingName,
    string CategoryCode, string CategoryName, string DesignationCode, string DesignationName,
    string FactoryCode, string FactoryName, string DepartmentCode, string DepartmentName, bool IsActive);
public sealed record EmployeeUpsertRequest(
    string Epf, string FullName, string? CallingName,
    string CategoryCode, string DesignationCode, string FactoryCode, string DepartmentCode);

public sealed record MobileAccountDto(
    Guid Id, string MobileNumber, string Epf, string EmployeeName,
    string Factory, string Department, decimal MonthlyCreditLimit, decimal MonthlyRental, bool IsActive);
public sealed record MobileAccountUpsertRequest(
    string MobileNumber,
    string EmployeeEpf,
    [property: System.Text.Json.Serialization.JsonRequired] decimal MonthlyCreditLimit,
    [property: System.Text.Json.Serialization.JsonRequired] decimal MonthlyRental);
public interface IMasterDataService
{
    Task<PagedResult<EmployeeDto>> GetEmployeesAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto> GetEmployeeAsync(Guid id, CancellationToken cancellationToken);
    Task<EmployeeDto> CreateEmployeeAsync(EmployeeUpsertRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto> UpdateEmployeeAsync(Guid id, EmployeeUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateEmployeeAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<MobileAccountDto>> GetMobileAccountsAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<MobileAccountDto> GetMobileAccountAsync(Guid id, CancellationToken cancellationToken);
    Task<MobileAccountDto> CreateMobileAccountAsync(MobileAccountUpsertRequest request, CancellationToken cancellationToken);
    Task<MobileAccountDto> UpdateMobileAccountAsync(Guid id, MobileAccountUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateMobileAccountAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ReferenceDataDto>> GetFactoriesAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> GetFactoryAsync(Guid id, CancellationToken cancellationToken);
    Task<ReferenceDataDto> CreateFactoryAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> UpdateFactoryAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateFactoryAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ReferenceDataDto>> GetDepartmentsAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> GetDepartmentAsync(Guid id, CancellationToken cancellationToken);
    Task<ReferenceDataDto> CreateDepartmentAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> UpdateDepartmentAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateDepartmentAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ReferenceDataDto>> GetDesignationsAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> GetDesignationAsync(Guid id, CancellationToken cancellationToken);
    Task<ReferenceDataDto> CreateDesignationAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> UpdateDesignationAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateDesignationAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ReferenceDataDto>> GetCategoriesAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> GetCategoryAsync(Guid id, CancellationToken cancellationToken);
    Task<ReferenceDataDto> CreateCategoryAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> UpdateCategoryAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateCategoryAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ReferenceDataDto>> GetProvidersAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> GetProviderAsync(Guid id, CancellationToken cancellationToken);
    Task<ReferenceDataDto> CreateProviderAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> UpdateProviderAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateProviderAsync(Guid id, CancellationToken cancellationToken);
}
