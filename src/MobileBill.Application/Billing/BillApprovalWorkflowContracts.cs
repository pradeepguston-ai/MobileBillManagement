using System.Text.Json.Serialization;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Billing;

public sealed record WorkflowCommentRequest(string? Comment);
public sealed record WorkflowDecisionRequest([property: JsonConverter(typeof(JsonStringEnumConverter))] ApprovalAction Action, string? Comment);
public sealed record BillWorkflowResult(Guid BatchId, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus Status);
public sealed record BillWorkflowCapabilitiesDto(bool CanSubmit, bool CanApprove, bool CanReject, bool CanReturnForCorrection, bool CanLock, string CurrentStage, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus Status);
