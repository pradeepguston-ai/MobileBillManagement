using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

public sealed class PasswordResetRequest : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string NewPasswordHash { get; set; }
    public PasswordResetStatus Status { get; set; } = PasswordResetStatus.Pending;
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
}
