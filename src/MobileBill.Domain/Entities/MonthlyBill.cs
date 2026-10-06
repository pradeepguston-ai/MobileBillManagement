using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

public sealed class MonthlyBill : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public Guid MobileAccountId { get; set; }
    public Guid BillLineId { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal MonthlyRental { get; set; }
    public decimal ActualBill { get; set; }
    public decimal Variance { get; set; }
    public decimal CalculatedExcess { get; set; }
    public decimal FinalDeduction { get; set; }
    public Responsibility? Responsibility { get; set; }
    public DateTimeOffset? AssessedAt { get; set; }
    public string? AssessedBy { get; set; }
    public decimal? DeductionOverrideAmount { get; set; }
    public string? DeductionOverrideReason { get; set; }
    public string? DeductionOverrideBy { get; set; }
    public DateTimeOffset? DeductionOverrideAt { get; set; }
    public string? Remark { get; set; }
    public MonthlyBillStatus Status { get; set; } = MonthlyBillStatus.Pending;
    public required string EmployeeEpfSnapshot { get; set; }
    public required string EmployeeNameSnapshot { get; set; }
    public string? CallingNameSnapshot { get; set; }
    public required string CategoryCodeSnapshot { get; set; }
    public string? CategoryNameSnapshot { get; set; }
    public required string DesignationCodeSnapshot { get; set; }
    public string? DesignationNameSnapshot { get; set; }
    public required string FactoryCodeSnapshot { get; set; }
    public string? FactoryNameSnapshot { get; set; }
    public required string DepartmentCodeSnapshot { get; set; }
    public string? DepartmentNameSnapshot { get; set; }
    public string? SectionCodeSnapshot { get; set; }
    public string? SectionNameSnapshot { get; set; }
    public string? SubSectionCodeSnapshot { get; set; }
    public string? SubSectionNameSnapshot { get; set; }
    public required string MobileNumberSnapshot { get; set; }
    // A company-paid bill for a SIM in the pool (or billed after disconnection); EmployeeId is the last holder.
    public bool IsPooled { get; set; }
    public DateOnly? EntitlementEffectiveFromSnapshot { get; set; }
    public DateOnly? EntitlementEffectiveToSnapshot { get; set; }
    public AllocationMatchMethod AllocationMatchMethod { get; set; } = AllocationMatchMethod.Automatic;
    public EntitlementMatchMethod EntitlementMatchMethod { get; set; } = EntitlementMatchMethod.Automatic;
    public Employee Employee { get; set; } = null!;
    public MobileAccount MobileAccount { get; set; } = null!;
    public BillLine BillLine { get; set; } = null!;
    public ICollection<BillException> Exceptions { get; } = new List<BillException>();
}
