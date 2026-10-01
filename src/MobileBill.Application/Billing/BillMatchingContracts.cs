using MobileBill.Domain.Enums;
using MobileBill.Application.Common;
using System.Text.Json.Serialization;

namespace MobileBill.Application.Billing;

public sealed record BillMatchingResult(Guid BillBatchId, int MonthlyBillsCreated, int ExceptionsCreated);

public sealed record BillExceptionDto(
    Guid Id,
    Guid BillBatchId,
    Guid? BillLineId,
    string? MobileNumber,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] BillExceptionType ExceptionType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] BillExceptionSeverity Severity,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] BillExceptionStatus Status,
    string Description,
    string? Resolution,
    string? ResolvedBy,
    DateTimeOffset? ResolvedAt,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] AllocationMatchMethod? AllocationMatchMethod,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] EntitlementMatchMethod? EntitlementMatchMethod);

public enum BillExceptionResolutionFilter { All, Unresolved, Resolved }

public sealed record BillExceptionListRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    BillExceptionType? ExceptionType = null,
    BillExceptionResolutionFilter Resolution = BillExceptionResolutionFilter.All)
{
    public int NormalizedPageNumber => Math.Max(1, PageNumber);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 100);
}

public sealed record BillExceptionSummaryDto(Guid BillBatchId, int TotalCount, int UnresolvedCount, int ResolvedCount);

public sealed record MobileAccountCandidateDto(
    Guid MobileAccountId,
    Guid EmployeeId,
    string MobileNumber,
    string EmployeeEpf,
    string EmployeeName,
    bool IsActive);

public sealed record ResolveMobileAccountExceptionRequest(Guid MobileAccountId, string Comment);

public sealed record BillExceptionResolutionResult(
    Guid BillExceptionId,
    BillExceptionStatus Status,
    Guid? MonthlyBillId,
    BillExceptionType? FollowUpExceptionType);

public interface IBillMatchingService
{
    Task<BillMatchingResult> MatchAsync(Guid billBatchId, CancellationToken cancellationToken);
}

public interface IBillExceptionReviewService
{
    Task<PagedResult<BillExceptionDto>> GetExceptionsAsync(Guid billBatchId, BillExceptionListRequest request, CancellationToken cancellationToken);
    Task<BillExceptionSummaryDto> GetSummaryAsync(Guid billBatchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MobileAccountCandidateDto>> GetMobileAccountCandidatesAsync(Guid billExceptionId, CancellationToken cancellationToken);
    Task<BillExceptionResolutionResult> ResolveMobileNotFoundAsync(Guid billExceptionId, ResolveMobileAccountExceptionRequest request, CancellationToken cancellationToken);
}
