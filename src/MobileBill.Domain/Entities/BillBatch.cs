using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

public sealed class BillBatch : AuditableEntity
{
    public Guid ProviderId { get; set; }
    public required string CorporateCode { get; set; }
    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }
    public string? OriginalFileName { get; set; }
    public string? StoredFilePath { get; set; }
    public string? FileHash { get; set; }
    public decimal? StatedGrandTotal { get; set; }
    public decimal? CalculatedGrandTotal { get; set; }
    public decimal? Difference { get; set; }
    public GrandTotalSource GrandTotalSource { get; set; } = GrandTotalSource.None;
    public ValidationLevel ValidationLevel { get; set; } = ValidationLevel.None;
    public string? ValidationWarning { get; set; }
    public string? ValidatedBy { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
    public BillBatchStatus Status { get; set; } = BillBatchStatus.Draft;
    public string? UploadedBy { get; set; }
    public DateTimeOffset? UploadedAt { get; set; }
    public TelecomProvider Provider { get; set; } = null!;
    public ICollection<BillLine> BillLines { get; } = new List<BillLine>();
    public ICollection<BillException> Exceptions { get; } = new List<BillException>();
    public ICollection<ApprovalHistory> ApprovalHistory { get; } = new List<ApprovalHistory>();
}
