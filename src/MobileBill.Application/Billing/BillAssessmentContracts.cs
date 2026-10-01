using MobileBill.Domain.Enums;
using System.Text.Json.Serialization;

namespace MobileBill.Application.Billing;

public sealed record AssessMonthlyBillRequest([property: JsonConverter(typeof(JsonStringEnumConverter))] Responsibility Responsibility, decimal? FinalDeduction, string? Reason);

public sealed record MonthlyBillAssessmentDto(
    Guid MonthlyBillId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] Responsibility? Responsibility,
    decimal ActualBill,
    decimal AvailableEntitlement,
    decimal Variance,
    decimal CalculatedExcess,
    decimal FinalDeduction,
    DateTimeOffset? AssessedAt,
    string? AssessedBy,
    decimal? DeductionOverrideAmount,
    string? DeductionOverrideReason,
    string? DeductionOverrideBy,
    DateTimeOffset? DeductionOverrideAt);

public sealed record BulkAssessMonthlyBillsRequest(IReadOnlyList<Guid> MonthlyBillIds, [property: JsonConverter(typeof(JsonStringEnumConverter))] Responsibility Responsibility, string? Reason);

public sealed record BulkAssessmentItemResult(Guid MonthlyBillId, bool Success, string? Error);

public sealed record BulkAssessmentResultDto(int SuccessCount, int FailureCount, IReadOnlyList<BulkAssessmentItemResult> Items);

public interface IBillAssessmentService
{
    Task<MonthlyBillAssessmentDto> AssessAsync(Guid monthlyBillId, AssessMonthlyBillRequest request, CancellationToken cancellationToken);
    Task<BulkAssessmentResultDto> BulkAssessAsync(BulkAssessMonthlyBillsRequest request, CancellationToken cancellationToken);
}
