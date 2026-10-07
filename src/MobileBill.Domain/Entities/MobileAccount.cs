using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

public sealed class MobileAccount : AuditableEntity
{
    public required string MobileNumber { get; set; }
    // The holder; for a Pooled or Disconnected SIM, the last employee who held it. Linked by the employee's ID
    // because an EPF number is unique only within a factory.
    public Guid EmployeeId { get; set; }
    public decimal MonthlyCreditLimit { get; set; }
    public decimal MonthlyRental { get; set; }
    // Active allocations are matched to bills; a Pooled SIM stays active, a Disconnected one does not.
    public bool IsActive { get; set; } = true;
    public SimStatus Status { get; set; } = SimStatus.Assigned;
    // The holder's resignation date; decides who pays the bill for that billing month (see SimPoolRules).
    public DateOnly? PooledOn { get; set; }
    public DateOnly? DisconnectedOn { get; set; }
    // Why the SIM was pooled or disconnected, for example "Resigned".
    public string? StatusReason { get; set; }
    // Optional; older allocations may not have one yet.
    public SimType? SimType { get; set; }
    // Required for new allocations; older allocations may not have one yet.
    public Guid? PackageId { get; set; }
    public MobilePackage? Package { get; set; }
    public Employee Employee { get; set; } = null!;
    public ICollection<MonthlyBill> MonthlyBills { get; } = new List<MonthlyBill>();
}
