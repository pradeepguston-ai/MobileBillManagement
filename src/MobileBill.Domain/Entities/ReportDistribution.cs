using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

// Who receives the monthly bill report by email, and which part of it: the report filtered to these factories and
// categories (none listed means all), sent as Excel and PDF attachments.
public sealed class ReportDistributionRule : AuditableEntity
{
    public required string Name { get; set; }
    // Comma-separated master codes; empty means every factory / every category.
    public string FactoryCodes { get; set; } = string.Empty;
    public string CategoryCodes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<ReportDistributionRecipient> Recipients { get; } = new List<ReportDistributionRecipient>();
}

// A system user (their current email address is used when sending) or a typed email address; exactly one is set.
public sealed class ReportDistributionRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public ReportDistributionRule Rule { get; set; } = null!;
    public User? User { get; set; }
}

// One report email for one batch and rule: the send log, and the queue the background sender works through.
// The rule's filters and recipients are copied when it is sent, so the log stays true if the rule changes later.
public sealed class ReportEmailDelivery : AuditableEntity
{
    public Guid BillBatchId { get; set; }
    public Guid? RuleId { get; set; }
    public required string RuleName { get; set; }
    public string FactoryCodes { get; set; } = string.Empty;
    public string CategoryCodes { get; set; } = string.Empty;
    // Semicolon-separated email addresses.
    public required string Recipients { get; set; }
    public ReportEmailStatus Status { get; set; } = ReportEmailStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? LastError { get; set; }
    public required string RequestedByName { get; set; }
    public BillBatch BillBatch { get; set; } = null!;
    public ReportDistributionRule? Rule { get; set; }
}
