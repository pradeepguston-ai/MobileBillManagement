using MobileBill.Domain.Common;

namespace MobileBill.Domain.Entities;

public sealed class AuditLog : AuditableEntity
{
    public required string EntityName { get; set; }
    public Guid EntityId { get; set; }
    public required string Action { get; set; }
    public string? BeforeDataJson { get; set; }
    public string? AfterDataJson { get; set; }
    public required string PerformedBy { get; set; }
    public DateTimeOffset PerformedAt { get; set; }
    public string? CorrelationId { get; set; }
}
