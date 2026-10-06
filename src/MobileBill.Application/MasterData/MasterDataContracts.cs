using MobileBill.Application.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.MasterData;

public sealed record ReferenceDataDto(Guid Id, string Code, string Name, bool IsActive);
public sealed record ReferenceDataUpsertRequest(string Code, string Name);

// Department → Section → Sub Section.
public sealed record SectionDto(Guid Id, string Code, string Name, string DepartmentCode, string DepartmentName, bool IsActive);
public sealed record SectionUpsertRequest(string Code, string Name, string DepartmentCode);
public sealed record SubSectionDto(Guid Id, string Code, string Name, string SectionCode, string SectionName, string DepartmentCode, string DepartmentName, bool IsActive);
public sealed record SubSectionUpsertRequest(string Code, string Name, string SectionCode);

public sealed record EmployeeDto(
    Guid Id, string Epf, string FullName, string? CallingName,
    string CategoryCode, string CategoryName, string DesignationCode, string DesignationName,
    string FactoryCode, string FactoryName, string DepartmentCode, string DepartmentName, bool IsActive,
    string? SectionCode = null, string? SectionName = null, string? SubSectionCode = null, string? SubSectionName = null);
// Section and Sub Section are optional; a Section must belong to the Department and a Sub Section to the Section.
public sealed record EmployeeUpsertRequest(
    string Epf, string FullName, string? CallingName,
    string CategoryCode, string DesignationCode, string FactoryCode, string DepartmentCode,
    string? SectionCode = null, string? SubSectionCode = null);

// For a Pooled or Disconnected SIM, Epf and EmployeeName are the last holder.
public sealed record MobileAccountDto(
    Guid Id, string MobileNumber, string Epf, string EmployeeName,
    string Factory, string Department, decimal MonthlyCreditLimit, decimal MonthlyRental, bool IsActive,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))] SimStatus Status = SimStatus.Assigned,
    DateOnly? PooledOn = null, DateOnly? DisconnectedOn = null, string? StatusReason = null, Guid EmployeeId = default, string? FactoryCode = null);

// SIM Pool: a resigned holder's number waits here for a new holder (see SimPoolRules for who pays meanwhile).
public sealed record ReleaseToPoolRequest(DateOnly ResignedOn, string? Reason);
// Credit limit and rental default to the pooled SIM's current amounts; only Administrator and IT Engineer may change them.
public sealed record AssignFromPoolRequest(Guid EmployeeId, decimal? MonthlyCreditLimit = null, decimal? MonthlyRental = null);
public sealed record DisconnectSimRequest(DateOnly DisconnectedOn, string? Reason);
// Releases every number the employee holds to the SIM Pool and deactivates the employee, in one step.
public sealed record ResignEmployeeRequest(DateOnly ResignedOn, string? Reason);
public sealed record SimPoolItemDto(
    Guid Id, string MobileNumber, string PreviousEpf, string PreviousEmployeeName, string Factory, string Department,
    DateOnly PooledOn, string? Reason, int DaysInPool, bool IsLongIdle, decimal MonthlyCreditLimit, decimal MonthlyRental);
// The holder is identified by employee ID, since the same EPF can exist in different factories.
public sealed record MobileAccountUpsertRequest(
    string MobileNumber,
    Guid EmployeeId,
    [property: System.Text.Json.Serialization.JsonRequired] decimal MonthlyCreditLimit,
    [property: System.Text.Json.Serialization.JsonRequired] decimal MonthlyRental);
public sealed record MobileAccountReassignRequest(Guid EmployeeId);
public interface IMasterDataService
{
    Task<PagedResult<EmployeeDto>> GetEmployeesAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto> GetEmployeeAsync(Guid id, CancellationToken cancellationToken);
    Task<EmployeeDto> CreateEmployeeAsync(EmployeeUpsertRequest request, CancellationToken cancellationToken);
    Task<EmployeeDto> UpdateEmployeeAsync(Guid id, EmployeeUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateEmployeeAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<MobileAccountDto>> GetMobileAccountsAsync(PagedRequest request, CancellationToken cancellationToken, SimStatus? status = null);
    Task<MobileAccountDto> GetMobileAccountAsync(Guid id, CancellationToken cancellationToken);
    Task<MobileAccountDto> CreateMobileAccountAsync(MobileAccountUpsertRequest request, CancellationToken cancellationToken);
    Task<MobileAccountDto> UpdateMobileAccountAsync(Guid id, MobileAccountUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateMobileAccountAsync(Guid id, CancellationToken cancellationToken);
    Task<MobileAccountDto> ReassignMobileAccountAsync(Guid id, MobileAccountReassignRequest request, CancellationToken cancellationToken);
    Task<MobileAccountDto> ReleaseToPoolAsync(Guid id, ReleaseToPoolRequest request, CancellationToken cancellationToken);
    Task<MobileAccountDto> AssignFromPoolAsync(Guid id, AssignFromPoolRequest request, CancellationToken cancellationToken);
    Task<MobileAccountDto> DisconnectSimAsync(Guid id, DisconnectSimRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<SimPoolItemDto>> GetSimPoolAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MobileAccountDto>> ResignEmployeeAsync(Guid employeeId, ResignEmployeeRequest request, CancellationToken cancellationToken);

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

    Task<PagedResult<SectionDto>> GetSectionsAsync(PagedRequest request, string? departmentCode, CancellationToken cancellationToken);
    Task<SectionDto> GetSectionAsync(Guid id, CancellationToken cancellationToken);
    Task<SectionDto> CreateSectionAsync(SectionUpsertRequest request, CancellationToken cancellationToken);
    Task<SectionDto> UpdateSectionAsync(Guid id, SectionUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateSectionAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<SubSectionDto>> GetSubSectionsAsync(PagedRequest request, string? sectionCode, CancellationToken cancellationToken);
    Task<SubSectionDto> GetSubSectionAsync(Guid id, CancellationToken cancellationToken);
    Task<SubSectionDto> CreateSubSectionAsync(SubSectionUpsertRequest request, CancellationToken cancellationToken);
    Task<SubSectionDto> UpdateSubSectionAsync(Guid id, SubSectionUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateSubSectionAsync(Guid id, CancellationToken cancellationToken);

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
