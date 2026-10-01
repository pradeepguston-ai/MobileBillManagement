using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

public sealed class BillException : AuditableEntity
{
    public Guid BillBatchId { get; set; }
    public Guid? BillLineId { get; set; }
    public Guid? MonthlyBillId { get; set; }
    public BillExceptionType ExceptionType { get; set; }
    public BillExceptionSeverity Severity { get; set; }
    public BillExceptionStatus Status { get; set; } = BillExceptionStatus.Open;
    public required string Description { get; set; }
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public BillBatch BillBatch { get; set; } = null!;
    public BillLine? BillLine { get; set; }
    public MonthlyBill? MonthlyBill { get; set; }
    public ICollection<BillExceptionResolution> Resolutions { get; } = new List<BillExceptionResolution>();
}
