using System.Text.Json.Serialization;
using MobileBill.Application.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Devices;

// Company mobile devices. Statuses and reasons travel as text ("InStock", "Resigned").
public sealed record MobileDeviceDto(
    Guid Id, string AssetTag, string Imei1, string? Imei2, string Brand, string Model, string? SerialNumber,
    DateOnly PurchaseDate, decimal PurchaseCost, DateOnly? WarrantyUntil, string? Supplier, string? Notes,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DeviceStatus Status, DateOnly StatusSince, int DaysInStatus,
    Guid? HolderEmployeeId, string? HolderEpf, string? HolderName, string? HolderFactory, string? HolderDepartment,
    bool IsActive, decimal CurrentValue);

public sealed record MobileDeviceUpsertRequest(
    string AssetTag, string Imei1, string? Imei2, string Brand, string Model, string? SerialNumber,
    DateOnly PurchaseDate, [property: JsonRequired] decimal PurchaseCost, DateOnly? WarrantyUntil, string? Supplier, string? Notes);

public sealed record IssueDeviceRequest(Guid EmployeeId, DateOnly IssuedOn, string? Notes);

// Condition is InStock, UnderRepair or Damaged. ChargeEmployee records the device's depreciated value for recovery
// (for example damage through negligence).
public sealed record ReturnDeviceRequest(
    DateOnly ReturnedOn,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DeviceStatus Condition,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DeviceReturnReason Reason,
    string? Notes, bool ChargeEmployee = false);

// Returns a damaged device (OldCondition UnderRepair or Damaged) and issues an In Stock device to the same employee, in one step.
public sealed record ReplaceDeviceRequest(
    Guid NewDeviceId, DateOnly ReplacedOn,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DeviceStatus OldCondition,
    string? Notes, bool ChargeEmployee = false);

// A lost device held by an employee has its depreciated value recorded for recovery unless ChargeEmployee is false.
public sealed record MarkDeviceLostRequest(DateOnly LostOn, string? Notes, bool ChargeEmployee = true);

// Repaired: UnderRepair or Damaged back to In Stock. Retire: an In Stock or Damaged device taken out of use.
public sealed record DeviceStatusChangeRequest(DateOnly On, string? Notes);

public sealed record DeviceIssueDto(
    Guid Id, Guid DeviceId, string AssetTag, string Brand, string Model,
    Guid EmployeeId, string Epf, string EmployeeName, string Factory, string Department,
    DateOnly IssuedOn, string? IssuedBy, string? IssueNotes, DateOnly? ReturnedOn,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DeviceStatus? ReturnCondition,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DeviceReturnReason? ReturnReason,
    string? ReturnNotes, string? ReplacementAssetTag, decimal? RecoverableAmount);

// A leaver's device still to be collected. Once overdue, RecoverableAmount is what may be recovered in the final settlement.
public sealed record DeviceToCollectDto(
    Guid DeviceId, string AssetTag, string Brand, string Model, string Imei1,
    Guid EmployeeId, string Epf, string EmployeeName, string Factory, string Department, DateOnly? ResignedOn,
    DateOnly ReturnPendingSince, int DaysWaiting, bool IsOverdue, decimal PurchaseCost, decimal RecoverableAmount);

public sealed record DeviceRegisterRequest(string? FactoryCode = null, string? DepartmentCode = null);

public sealed record DeviceReportFile(string FileName, string ContentType, byte[] Content);

public interface IDeviceService
{
    // IsActive true: devices in use or in stock; false: lost or retired.
    Task<PagedResult<MobileDeviceDto>> GetDevicesAsync(PagedRequest request, CancellationToken cancellationToken, DeviceStatus? status = null);
    Task<MobileDeviceDto> GetDeviceAsync(Guid id, CancellationToken cancellationToken);
    Task<MobileDeviceDto> CreateDeviceAsync(MobileDeviceUpsertRequest request, CancellationToken cancellationToken);
    Task<MobileDeviceDto> UpdateDeviceAsync(Guid id, MobileDeviceUpsertRequest request, CancellationToken cancellationToken);
    Task<MobileDeviceDto> IssueAsync(Guid id, IssueDeviceRequest request, CancellationToken cancellationToken);
    Task<MobileDeviceDto> ReturnAsync(Guid id, ReturnDeviceRequest request, CancellationToken cancellationToken);
    // Returns the new device.
    Task<MobileDeviceDto> ReplaceAsync(Guid id, ReplaceDeviceRequest request, CancellationToken cancellationToken);
    Task<MobileDeviceDto> MarkLostAsync(Guid id, MarkDeviceLostRequest request, CancellationToken cancellationToken);
    Task<MobileDeviceDto> MarkRepairedAsync(Guid id, DeviceStatusChangeRequest request, CancellationToken cancellationToken);
    Task<MobileDeviceDto> RetireAsync(Guid id, DeviceStatusChangeRequest request, CancellationToken cancellationToken);
    Task<PagedResult<DeviceIssueDto>> GetIssuesAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceToCollectDto>> GetToCollectAsync(CancellationToken cancellationToken);
    Task<DeviceReportFile> ExportRegisterExcelAsync(DeviceRegisterRequest request, CancellationToken cancellationToken);
    Task<DeviceReportFile> ExportRegisterPdfAsync(DeviceRegisterRequest request, CancellationToken cancellationToken);
}
