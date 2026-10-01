using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Billing;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Billing;

public sealed class BillApprovalWorkflowServiceTests
{
    [Fact]
    public async Task Sequential_approvals_record_server_roles_and_finance_completes_without_locking()
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db);
        var clock = new TestClock();
        var service = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), clock);

        await service.SubmitAsync(batch.Id, new WorkflowCommentRequest("Ready for IT review"), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, "IT checked"), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, "HR approved"), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, "Finance approved"), default);

        Assert.Equal(BillBatchStatus.Completed, (await db.BillBatches.SingleAsync()).Status);
        var approvals = await db.ApprovalHistories.OrderBy(x => x.ApprovedAt).ToListAsync();
        Assert.Equal(new[] { WorkflowRole.ITReviewer, WorkflowRole.ITReviewer, WorkflowRole.HRApprover, WorkflowRole.FinanceApprover }, approvals.Select(x => x.WorkflowRole));
        Assert.All(approvals, x => { Assert.Equal("dev-user", x.UserId); Assert.Equal("Development User", x.DisplayName); Assert.Equal(clock.UtcNow, x.ApprovedAt); });
    }

    [Fact]
    public async Task Authorized_user_can_lock_only_completed_batch_when_all_prerequisites_pass_and_lock_is_audited()
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db, BillBatchStatus.Completed);
        db.ApprovalHistories.Add(new ApprovalHistory { BillBatchId = batch.Id, Stage = ApprovalStage.FinanceApproval, WorkflowRole = WorkflowRole.FinanceApprover, Action = ApprovalAction.Approve, Decision = ApprovalDecision.Approved, UserId = "dev-user", DisplayName = "Development User", ApprovedBy = "Development User", ApprovedAt = DateTimeOffset.UnixEpoch, PreviousStatus = BillBatchStatus.FinanceApproval, NewStatus = BillBatchStatus.Completed });
        db.MonthlyBills.Add(new MonthlyBill { EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = Guid.NewGuid(), EmployeeEpfSnapshot = "E", EmployeeNameSnapshot = "Employee", MobileNumberSnapshot = "7", CategoryCodeSnapshot = "CAT", DesignationCodeSnapshot = "DES", FactoryCodeSnapshot = "FAC", DepartmentCodeSnapshot = "DEP", EntitlementEffectiveFromSnapshot = new DateOnly(2026, 9, 1), AssessedAt = DateTimeOffset.UnixEpoch });
        await db.SaveChangesAsync();
        var clock = new TestClock();

        await new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), clock).LockAsync(batch.Id, default);

        Assert.Equal(BillBatchStatus.Locked, (await db.BillBatches.SingleAsync()).Status);
        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("Locked", audit.Action);
        Assert.Equal("dev-user", audit.PerformedBy);
        Assert.Equal(clock.UtcNow, audit.PerformedAt);
    }

    [Fact]
    public async Task Workflow_rejects_skipped_stage_and_denied_action_without_history()
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db);
        var denied = new EfBillApprovalWorkflowService(db, new DenyWorkflowAuthorization(), new TestUser(), new TestClock());
        await Assert.ThrowsAsync<BillReviewForbiddenException>(() => denied.SubmitAsync(batch.Id, new WorkflowCommentRequest(null), default));
        Assert.Empty(await db.ApprovalHistories.ToListAsync());

        var allowed = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), new TestClock());
        await Assert.ThrowsAsync<BillWorkflowConflictException>(() => allowed.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, null), default));
    }

    [Theory]
    [InlineData(ApprovalAction.Reject, null)]
    [InlineData(ApprovalAction.Reject, "   ")]
    [InlineData(ApprovalAction.ReturnForCorrection, null)]
    [InlineData(ApprovalAction.ReturnForCorrection, "no")]
    public async Task Reject_and_return_require_a_meaningful_reason(ApprovalAction action, string? comment)
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db, BillBatchStatus.ITReview);
        var service = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), new TestClock());

        await Assert.ThrowsAsync<BillWorkflowValidationException>(() => service.DecideAsync(batch.Id, new WorkflowDecisionRequest(action, comment), default));

        Assert.Equal(BillBatchStatus.ITReview, (await db.BillBatches.SingleAsync()).Status);
        Assert.Empty(await db.ApprovalHistories.ToListAsync());
    }

    [Fact]
    public async Task Submit_and_approve_allow_an_omitted_comment()
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db);
        var service = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), new TestClock());

        await service.SubmitAsync(batch.Id, new WorkflowCommentRequest(null), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, null), default);

        Assert.Equal(BillBatchStatus.HRApproval, (await db.BillBatches.SingleAsync()).Status);
        Assert.All(await db.ApprovalHistories.ToListAsync(), history => Assert.Null(history.Comment));
    }

    private static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<BillBatch> AddBatchAsync(MobileBillDbContext db, BillBatchStatus status = BillBatchStatus.Validated)
    {
        var batch = new BillBatch { ProviderId = Guid.NewGuid(), CorporateCode = "C", BillingYear = 2026, BillingMonth = 9, Status = status, ValidationLevel = ValidationLevel.StructuralOnly };
        db.BillBatches.Add(batch); await db.SaveChangesAsync(); return batch;
    }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero); }
    private sealed class TestUser : ICurrentUserService { public string UserId => "dev-user"; public string DisplayName => "Development User"; public UserRole Role => UserRole.Administrator; }
    private sealed class AllowWorkflowAuthorization : IBillReviewAuthorizationService { public Task<bool> CanResolveExceptionsAsync(CancellationToken cancellationToken) => Task.FromResult(true); public Task<bool> AuthorizeAsync(BillWorkflowAction action, CancellationToken cancellationToken) => Task.FromResult(true); }
    private sealed class DenyWorkflowAuthorization : IBillReviewAuthorizationService { public Task<bool> CanResolveExceptionsAsync(CancellationToken cancellationToken) => Task.FromResult(false); public Task<bool> AuthorizeAsync(BillWorkflowAction action, CancellationToken cancellationToken) => Task.FromResult(false); }
}
