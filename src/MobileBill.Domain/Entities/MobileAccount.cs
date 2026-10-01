using MobileBill.Domain.Common;

namespace MobileBill.Domain.Entities;

public sealed class MobileAccount : AuditableEntity
{
    public required string MobileNumber { get; set; }
    public required string EmployeeEpf { get; set; }
    public decimal MonthlyCreditLimit { get; set; }
    public decimal MonthlyRental { get; set; }
    public bool IsActive { get; set; } = true;
    public Employee Employee { get; set; } = null!;
    public ICollection<MonthlyBill> MonthlyBills { get; } = new List<MonthlyBill>();
}
