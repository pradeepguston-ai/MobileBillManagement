namespace MobileBill.Domain.Entities;

// One thing a user did in the system, in plain words (for example "Approved the October 2026 bill batch (HR approval)"),
// recorded when the request succeeded. Shown on the Activity Log screen to every signed-in user, so it never holds
// passwords, assistant questions or other private content. Detailed before/after changes stay in AuditLogs.
public sealed class ActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; }
    public required string UserId { get; set; }
    // As the user was named, and their role, at the time.
    public required string UserName { get; set; }
    public required string Role { get; set; }
    // The part of the system, for example "Billing", "Approvals", "Employees".
    public required string Area { get; set; }
    public required string Description { get; set; }
    public string? TargetId { get; set; }
    public required string HttpMethod { get; set; }
    public required string Path { get; set; }
}
