using System.Text.Json.Serialization;
using MobileBill.Application.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Reports;

// A recipient is a system user (sent to their current email address) or a typed email address (UserId empty).
// Inactive: a user whose account is no longer active; they are skipped when sending.
public sealed record ReportRecipientDto(Guid? UserId, string Email, string? DisplayName, bool Inactive);

// Empty FactoryCodes / CategoryCodes mean every factory / every category.
public sealed record ReportDistributionRuleDto(Guid Id, string Name, IReadOnlyList<string> FactoryCodes, string FactoryLabel,
    IReadOnlyList<string> CategoryCodes, string CategoryLabel, IReadOnlyList<ReportRecipientDto> Recipients, bool IsActive);

public sealed record SaveReportDistributionRuleRequest(string? Name, IReadOnlyList<string>? FactoryCodes, IReadOnlyList<string>? CategoryCodes,
    IReadOnlyList<Guid>? UserIds, IReadOnlyList<string>? Emails, bool IsActive = true);

public sealed record ReportRecipientOptionDto(Guid UserId, string DisplayName, string Email, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole Role);

// One active rule for a batch before sending: who would receive it and how many numbers its report has.
public sealed record ReportEmailPreviewDto(Guid RuleId, string RuleName, string FactoryLabel, string CategoryLabel,
    IReadOnlyList<string> Recipients, int NumberCount, bool CanSend, string? Reason);

public sealed record SendReportEmailsRequest(IReadOnlyList<Guid>? RuleIds);

public sealed record ReportEmailDeliveryDto(Guid Id, Guid? RuleId, string RuleName, string Recipients,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ReportEmailStatus Status, int Attempts,
    DateTimeOffset RequestedAt, string RequestedBy, DateTimeOffset? SentAt, string? LastError,
    Guid BillBatchId = default, int BillingYear = 0, int BillingMonth = 0);

// The email history across batches; Search matches the rule name or a recipient address.
public sealed record ReportEmailHistoryRequest(Guid? BatchId = null, ReportEmailStatus? Status = null, string? Search = null, int PageNumber = 1, int PageSize = 20);

// Email distribution of the monthly bill report: the rules (who gets which factories and categories) and sending a batch's reports.
public interface IReportDistributionService
{
    Task<IReadOnlyList<ReportDistributionRuleDto>> ListRulesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReportRecipientOptionDto>> GetRecipientOptionsAsync(CancellationToken cancellationToken);
    Task<ReportDistributionRuleDto> CreateRuleAsync(SaveReportDistributionRuleRequest request, CancellationToken cancellationToken);
    Task<ReportDistributionRuleDto> UpdateRuleAsync(Guid id, SaveReportDistributionRuleRequest request, CancellationToken cancellationToken);
    Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportEmailPreviewDto>> PreviewAsync(Guid batchId, CancellationToken cancellationToken);
    // Queues one email per chosen rule; the background sender builds the reports and sends them.
    Task<IReadOnlyList<ReportEmailDeliveryDto>> SendAsync(Guid batchId, SendReportEmailsRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReportEmailDeliveryDto>> ListDeliveriesAsync(Guid batchId, CancellationToken cancellationToken);
    Task<ReportEmailDeliveryDto> ResendAsync(Guid deliveryId, CancellationToken cancellationToken);
    Task<PagedResult<ReportEmailDeliveryDto>> SearchDeliveriesAsync(ReportEmailHistoryRequest request, CancellationToken cancellationToken);
}

// When a batch is locked: queues the report email for every active rule that can be sent and has not already been emailed
// for the batch. Does nothing while email or this option (Email:SendReportsOnLock) is off. Never throws: the lock stands either way.
public interface IReportLockDistribution
{
    Task QueueOnLockAsync(Guid batchId, CancellationToken cancellationToken);
}

// Sends the next queued report email that is due; false when nothing is waiting.
public interface IReportEmailProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}

public sealed class ReportDistributionNotFoundException(string message) : Exception(message);
public sealed class ReportDistributionValidationException(string message) : Exception(message);
public sealed class ReportDistributionConflictException(string message) : Exception(message);
