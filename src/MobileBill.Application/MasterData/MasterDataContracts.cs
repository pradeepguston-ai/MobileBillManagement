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
    string? SectionCode = null, string? SectionName = null, string? SubSectionCode = null, string? SubSectionName = null,
    DateOnly? ResignedOn = null);
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
    DateOnly? PooledOn = null, DateOnly? DisconnectedOn = null, string? StatusReason = null, Guid EmployeeId = default, string? FactoryCode = null,
    Guid? PackageId = null, string? PackageCode = null, bool DiffersFromPackage = false,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))] SimType? SimType = null);

// A provider's mobile plan. Rental is before tax; Total With Tax is for reference. Choosing a package for an
// allocation fills in its rental and suggests the credit limit.
public sealed record MobilePackageDto(
    Guid Id, string Code, Guid ProviderId, string ProviderName, string Description,
    decimal MonthlyRental, decimal TotalWithTax, decimal DefaultCreditLimit, bool IsActive, int AllocationCount, int AllocationsWithOtherRental);
public sealed record MobilePackageUpsertRequest(
    string Code, Guid ProviderId, string Description,
    [property: System.Text.Json.Serialization.JsonRequired] decimal MonthlyRental,
    [property: System.Text.Json.Serialization.JsonRequired] decimal TotalWithTax,
    [property: System.Text.Json.Serialization.JsonRequired] decimal DefaultCreditLimit);

// SIM Pool: a resigned holder's number waits here for a new holder (see SimPoolRules for who pays meanwhile).
public sealed record ReleaseToPoolRequest(DateOnly ResignedOn, string? Reason);
// Package, credit limit and rental default to the pooled SIM's current ones.
public sealed record AssignFromPoolRequest(Guid EmployeeId, decimal? MonthlyCreditLimit = null, decimal? MonthlyRental = null, Guid? PackageId = null);
public sealed record DisconnectSimRequest(DateOnly DisconnectedOn, string? Reason);
// A date up to today releases every number the employee holds to the SIM Pool and deactivates the employee, in
// one step. A future date records a pending resignation, completed automatically on that date.
public sealed record ResignEmployeeRequest(DateOnly ResignedOn, string? Reason);
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ResignationStatus { Pending, Resigned }
// MobileNumbers: numbers the employee still holds (pending), or their numbers still waiting in the SIM Pool (resigned).
// Devices: company devices they hold (pending), or devices still to be collected from them (resigned), as "Asset ID (Brand Model)".
public sealed record EmployeeResignationDto(
    Guid Id, string Epf, string FullName, string? CallingName, string Factory, string Department, string? Section,
    DateOnly ResignedOn, string? Reason, int DaysLeft, IReadOnlyList<string> MobileNumbers, IReadOnlyList<string>? Devices = null);
public sealed record SimPoolItemDto(
    Guid Id, string MobileNumber, string PreviousEpf, string PreviousEmployeeName, string Factory, string Department,
    DateOnly PooledOn, string? Reason, int DaysInPool, bool IsLongIdle, decimal MonthlyCreditLimit, decimal MonthlyRental);
// The holder is identified by employee ID, since the same EPF can exist in different factories.
// A package is required for a new allocation, and once an allocation has one it cannot be removed.
public sealed record MobileAccountUpsertRequest(
    string MobileNumber,
    Guid EmployeeId,
    [property: System.Text.Json.Serialization.JsonRequired] decimal MonthlyCreditLimit,
    [property: System.Text.Json.Serialization.JsonRequired] decimal MonthlyRental,
    Guid? PackageId = null,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))] SimType? SimType = null);
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
    Task CancelResignationAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeResignationDto>> GetResignationsAsync(ResignationStatus status, CancellationToken cancellationToken);
    // Completes every pending resignation whose date has been reached; returns how many were completed.
    Task<int> CompleteDueResignationsAsync(CancellationToken cancellationToken);

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

    Task<PagedResult<MobilePackageDto>> GetPackagesAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<MobilePackageDto> GetPackageAsync(Guid id, CancellationToken cancellationToken);
    Task<MobilePackageDto> CreatePackageAsync(MobilePackageUpsertRequest request, CancellationToken cancellationToken);
    Task<MobilePackageDto> UpdatePackageAsync(Guid id, MobilePackageUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivatePackageAsync(Guid id, CancellationToken cancellationToken);
    // Sets the package's rental on every active allocation that uses it; credit limits stay per person. Returns how many changed.
    Task<int> ApplyPackageRentalAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ReferenceDataDto>> GetProvidersAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> GetProviderAsync(Guid id, CancellationToken cancellationToken);
    Task<ReferenceDataDto> CreateProviderAsync(ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task<ReferenceDataDto> UpdateProviderAsync(Guid id, ReferenceDataUpsertRequest request, CancellationToken cancellationToken);
    Task DeactivateProviderAsync(Guid id, CancellationToken cancellationToken);
}
