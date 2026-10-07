using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

// A company-issued mobile device, tracked by employee (not by mobile number). Its handovers are in Issues.
public sealed class MobileDevice : AuditableEntity
{
    public required string AssetTag { get; set; }
    public required string Imei1 { get; set; }
    public string? Imei2 { get; set; }
    public required string Brand { get; set; }
    public required string Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public decimal PurchaseCost { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    public string? Supplier { get; set; }
    public string? Notes { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.InStock;
    // When the device entered its current status, for example the day it went for repair or its holder resigned.
    public DateOnly StatusSince { get; set; }
    // The employee who has it now (Issued or ReturnPending); null otherwise.
    public Guid? CurrentEmployeeId { get; set; }
    public Employee? CurrentEmployee { get; set; }
    public ICollection<DeviceIssue> Issues { get; } = new List<DeviceIssue>();
}

// One handover of a device to an employee, open until the device comes back (or is lost).
public sealed class DeviceIssue : AuditableEntity
{
    public Guid DeviceId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly IssuedOn { get; set; }
    public string? IssueNotes { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    // The device's state when it came back: InStock, UnderRepair, Damaged or Lost.
    public DeviceStatus? ReturnCondition { get; set; }
    public DeviceReturnReason? ReturnReason { get; set; }
    public string? ReturnNotes { get; set; }
    // The device given in its place, when this one was replaced.
    public Guid? ReplacementDeviceId { get; set; }
    // What may be recovered from the employee (lost, damaged through negligence, or not returned); see DeviceRecoveryRules.
    public decimal? RecoverableAmount { get; set; }
    public MobileDevice Device { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public MobileDevice? ReplacementDevice { get; set; }
}
