using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

public sealed class BillLine : AuditableEntity
{
    public Guid BillBatchId { get; set; }
    public required string MobileNumber { get; set; }
    public int PageNumber { get; set; }
    public required string RawText { get; set; }
    public BillLineExtractionStatus ExtractionStatus { get; set; } = BillLineExtractionStatus.Pending;
    public string? ExtractionError { get; set; }
    public decimal PreviousDueAmount { get; set; }
    public decimal Payments { get; set; }
    public decimal TotalUsageCharges { get; set; }
    public decimal Idd { get; set; }
    public decimal Roaming { get; set; }
    public decimal ValueAddedServices { get; set; }
    public decimal Discounts { get; set; }
    public decimal BillAdjustmentsBalanceTransfers { get; set; }
    public decimal CommitmentCharges { get; set; }
    public decimal LatePaymentCharges { get; set; }
    public decimal AddToBill { get; set; }
    public decimal InstalmentPlans { get; set; }
    public decimal GovernmentTaxesAndLevies { get; set; }
    public decimal Vat { get; set; }
    public decimal ChargesForBillPeriod { get; set; }
    public decimal TotalDueAmount { get; set; }
    public BillBatch BillBatch { get; set; } = null!;
    public MonthlyBill? MonthlyBill { get; set; }
    public ICollection<BillException> Exceptions { get; } = new List<BillException>();
}
