using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

public sealed class ApprovalHistory : AuditableEntity
{
    public Guid BillBatchId { get; set; }
    public ApprovalStage Stage { get; set; }
    public WorkflowRole WorkflowRole { get; set; }
    public ApprovalAction Action { get; set; }
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public string? Comment { get; set; }
    public required string UserId { get; set; }
    public required string DisplayName { get; set; }
    public BillBatchStatus PreviousStatus { get; set; }
    public BillBatchStatus NewStatus { get; set; }
    public required string ApprovedBy { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }
    public BillBatch BillBatch { get; set; } = null!;
}
