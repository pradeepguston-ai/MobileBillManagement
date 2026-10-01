using MobileBill.Domain.Common;

namespace MobileBill.Domain.Entities;

public sealed class Employee : AuditableEntity
{
    public required string EPF { get; set; }
    public required string FullName { get; set; }
    public string? CallingName { get; set; }
    public required string CategoryCode { get; set; }
    public required string DesignationCode { get; set; }
    public required string FactoryCode { get; set; }
    public required string DepartmentCode { get; set; }
    public bool IsActive { get; set; } = true;
    public EmployeeCategory Category { get; set; } = null!;
    public Designation Designation { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<MobileAccount> MobileAccounts { get; } = new List<MobileAccount>();
    public ICollection<MonthlyBill> MonthlyBills { get; } = new List<MonthlyBill>();
}
