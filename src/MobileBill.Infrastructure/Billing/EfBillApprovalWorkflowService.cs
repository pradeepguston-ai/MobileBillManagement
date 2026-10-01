using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Billing;

public sealed class EfBillApprovalWorkflowService(MobileBillDbContext db, IBillReviewAuthorizationService authorization, ICurrentUserService currentUser, IClock clock) : IBillApprovalWorkflowService
{
    public async Task<BillWorkflowCapabilitiesDto> GetCapabilitiesAsync(Guid batchId, CancellationToken token)
    {
        var batch = await Find(batchId, token);
        var action = batch.Status switch
        {
            BillBatchStatus.Validated => BillWorkflowAction.Submit,
            BillBatchStatus.ITReview => BillWorkflowAction.ApproveIt,
            BillBatchStatus.HRApproval => BillWorkflowAction.ApproveHr,
            BillBatchStatus.FinanceApproval => BillWorkflowAction.ApproveFinance,
            BillBatchStatus.Completed => BillWorkflowAction.Lock,
            _ => (BillWorkflowAction?)null
        };
        var authorized = action is not null && await authorization.AuthorizeAsync(action.Value, token);
        var canDecide = authorized && batch.Status is BillBatchStatus.ITReview or BillBatchStatus.HRApproval or BillBatchStatus.FinanceApproval;
        var currentStage = batch.Status switch
        {
            BillBatchStatus.Validated => "Validation",
            BillBatchStatus.ITReview => "ITReview",
            BillBatchStatus.HRApproval => "HRApproval",
            BillBatchStatus.FinanceApproval => "FinanceApproval",
            BillBatchStatus.Completed => "Completed",
            BillBatchStatus.Locked => "Locked",
            _ => batch.Status.ToString()
        };
        return new BillWorkflowCapabilitiesDto(
            authorized && batch.Status == BillBatchStatus.Validated,
            canDecide,
            canDecide,
            canDecide,
            authorized && batch.Status == BillBatchStatus.Completed,
            currentStage,
            batch.Status);
    }

    public async Task<BillWorkflowResult> SubmitAsync(Guid batchId, WorkflowCommentRequest request, CancellationToken token)
    {
        await RequireAuthorization(BillWorkflowAction.Submit, token);
        var batch = await Find(batchId, token);
        Require(batch.Status == BillBatchStatus.Validated, "Only a Validated batch can be submitted for IT review.");
        Require(batch.ValidationLevel != ValidationLevel.None, "A batch must pass validation before IT submission.");
        return await TransitionWithHistory(batch, ApprovalStage.ITReview, WorkflowRole.ITReviewer, ApprovalAction.Submit, ApprovalDecision.Pending, BillBatchStatus.ITReview, request.Comment, token);
    }

    public async Task<BillWorkflowResult> DecideAsync(Guid batchId, WorkflowDecisionRequest request, CancellationToken token)
    {
        if (request.Action is not (ApprovalAction.Approve or ApprovalAction.Reject or ApprovalAction.ReturnForCorrection))
            throw new BillWorkflowValidationException("A workflow decision must be Approve, Reject, or ReturnForCorrection.");
        if (request.Action is ApprovalAction.Reject or ApprovalAction.ReturnForCorrection && !Meaningful(request.Comment))
            throw new BillWorkflowValidationException("A meaningful reason is required when rejecting or returning a batch for correction.");
        var batch = await Find(batchId, token);
        var stage = batch.Status switch
        {
            BillBatchStatus.ITReview => (ApprovalStage.ITReview, WorkflowRole.ITReviewer, BillWorkflowAction.ApproveIt, BillBatchStatus.HRApproval),
            BillBatchStatus.HRApproval => (ApprovalStage.HRApproval, WorkflowRole.HRApprover, BillWorkflowAction.ApproveHr, BillBatchStatus.FinanceApproval),
            BillBatchStatus.FinanceApproval => (ApprovalStage.FinanceApproval, WorkflowRole.FinanceApprover, BillWorkflowAction.ApproveFinance, BillBatchStatus.Completed),
            _ => throw new BillWorkflowConflictException("No approval decision is valid for the batch's current workflow status.")
        };
        await RequireAuthorization(stage.Item3, token);
        var decision = request.Action switch { ApprovalAction.Approve => ApprovalDecision.Approved, ApprovalAction.Reject => ApprovalDecision.Rejected, _ => ApprovalDecision.ReturnedForCorrection };
        var next = request.Action == ApprovalAction.Approve ? stage.Item4 : BillBatchStatus.Validated;
        return await TransitionWithHistory(batch, stage.Item1, stage.Item2, request.Action, decision, next, request.Comment, token);
    }

    public async Task<BillWorkflowResult> LockAsync(Guid batchId, CancellationToken token)
    {
        await RequireAuthorization(BillWorkflowAction.Lock, token);
        var batch = await Find(batchId, token);
        Require(batch.Status == BillBatchStatus.Completed, "Only a Completed batch can be locked.");
        Require(batch.ValidationLevel != ValidationLevel.None, "A batch with failed validation cannot be locked.");
        Require(!await db.BillExceptions.AnyAsync(x => x.BillBatchId == batchId && x.Severity == BillExceptionSeverity.Blocking && x.Status != BillExceptionStatus.Resolved && x.Status != BillExceptionStatus.Waived, token), "Resolve or waive all blocking exceptions before locking.");
        Require(!await db.MonthlyBills.AnyAsync(x => x.BillLine.BillBatchId == batchId && x.AssessedAt == null, token), "All monthly bills must be assessed before locking.");
        Require(await db.ApprovalHistories.AnyAsync(x => x.BillBatchId == batchId && x.Stage == ApprovalStage.FinanceApproval && x.Action == ApprovalAction.Approve && x.Decision == ApprovalDecision.Approved, token), "A Finance approval is required before locking.");
        var now = clock.UtcNow;
        var previous = batch.Status;
        batch.Status = BillBatchStatus.Locked;
        batch.UpdatedAtUtc = now;
        batch.UpdatedBy = currentUser.UserId;
        db.AuditLogs.Add(new AuditLog { EntityName = nameof(BillBatch), EntityId = batch.Id, Action = "Locked", BeforeDataJson = $"{{\"Status\":\"{previous}\"}}", AfterDataJson = $"{{\"Status\":\"{BillBatchStatus.Locked}\",\"WorkflowRole\":\"{WorkflowRole.PeriodLocker}\"}}", PerformedBy = currentUser.UserId, PerformedAt = now, CreatedAtUtc = now, CreatedBy = currentUser.UserId });
        await db.SaveChangesAsync(token);
        return new BillWorkflowResult(batch.Id, batch.Status);
    }

    private async Task<BillWorkflowResult> TransitionWithHistory(BillBatch batch, ApprovalStage stage, WorkflowRole role, ApprovalAction action, ApprovalDecision decision, BillBatchStatus next, string? comment, CancellationToken token)
    {
        var now = clock.UtcNow;
        var previous = batch.Status;
        batch.Status = next;
        batch.UpdatedAtUtc = now;
        batch.UpdatedBy = currentUser.UserId;
        db.ApprovalHistories.Add(new ApprovalHistory { BillBatchId = batch.Id, Stage = stage, WorkflowRole = role, Action = action, Decision = decision, Comment = Trim(comment), UserId = currentUser.UserId, DisplayName = currentUser.DisplayName, ApprovedBy = currentUser.DisplayName, ApprovedAt = now, PreviousStatus = previous, NewStatus = next, CreatedAtUtc = now, CreatedBy = currentUser.UserId });
        await db.SaveChangesAsync(token);
        return new BillWorkflowResult(batch.Id, next);
    }

    private async Task RequireAuthorization(BillWorkflowAction action, CancellationToken token)
    {
        if (!await authorization.AuthorizeAsync(action, token)) throw new BillReviewForbiddenException();
    }
    private async Task<BillBatch> Find(Guid id, CancellationToken token) => await db.BillBatches.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new BillBatchNotFoundException(id);
    private static void Require(bool condition, string message) { if (!condition) throw new BillWorkflowConflictException(message); }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool Meaningful(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 5;
}
