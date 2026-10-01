using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;
namespace MobileBill.Domain.Entities;
public sealed class BillExceptionResolution : AuditableEntity
{
 public Guid BillExceptionId { get; set; } public Guid BillLineId { get; set; } public Guid MobileAccountId { get; set; } public Guid EmployeeId { get; set; }
 public required string MobileNumber { get; set; } public required string EmployeeEpf { get; set; } public required string EmployeeName { get; set; }
 public BillExceptionType OriginalExceptionType { get; set; }
 public int BillingYear { get; set; } public int BillingMonth { get; set; } public required string ResolutionComment { get; set; } public required string ResolvedBy { get; set; } public DateTimeOffset ResolvedAt { get; set; }
 public BillException BillException { get; set; } = null!;
}
