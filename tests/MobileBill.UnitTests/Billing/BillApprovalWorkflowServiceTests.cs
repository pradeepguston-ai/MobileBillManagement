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

    [Theory]
    [InlineData(BillExceptionType.MOBILE_NOT_FOUND, BillExceptionStatus.Open, false)]
    [InlineData(BillExceptionType.MULTIPLE_ACTIVE_ALLOCATIONS, BillExceptionStatus.InReview, false)]
    // A resigned (inactive) employee's number does not stop the submission, nor does an exception already dealt with.
    [InlineData(BillExceptionType.EMPLOYEE_NOT_ACTIVE, BillExceptionStatus.Open, true)]
    [InlineData(BillExceptionType.MOBILE_NOT_FOUND, BillExceptionStatus.Resolved, true)]
    [InlineData(BillExceptionType.MOBILE_NOT_FOUND, BillExceptionStatus.Waived, true)]
    public async Task Submit_is_refused_while_numbers_in_exception_review_are_not_assigned(BillExceptionType type, BillExceptionStatus status, bool allowed)
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db);
        var lineId = Guid.NewGuid();
        db.BillExceptions.Add(new BillException { BillBatchId = batch.Id, BillLineId = lineId, ExceptionType = type, Severity = BillExceptionSeverity.Blocking, Status = status, Description = "x" });
        if (type == BillExceptionType.EMPLOYEE_NOT_ACTIVE)
            db.BillExceptions.Add(new BillException { BillBatchId = batch.Id, BillLineId = lineId, ExceptionType = BillExceptionType.ZERO_BILL, Severity = BillExceptionSeverity.Information, Description = "x" });
        await db.SaveChangesAsync();
        var service = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), new TestClock());

        if (allowed)
        {
            await service.SubmitAsync(batch.Id, new WorkflowCommentRequest(null), default);
            Assert.Equal(BillBatchStatus.ITReview, (await db.BillBatches.SingleAsync()).Status);
            return;
        }
        var refusal = await Assert.ThrowsAsync<BillWorkflowConflictException>(() => service.SubmitAsync(batch.Id, new WorkflowCommentRequest(null), default));
        Assert.Contains("1 number(s) in Exception Review are not assigned", refusal.Message);
        Assert.Equal(BillBatchStatus.Validated, (await db.BillBatches.SingleAsync()).Status);
        Assert.Empty(await db.ApprovalHistories.ToListAsync());
    }

    [Fact]
    public async Task Submit_names_unassigned_numbers_and_numbers_without_responsibility_together()
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db);
        db.BillExceptions.Add(new BillException { BillBatchId = batch.Id, BillLineId = Guid.NewGuid(), ExceptionType = BillExceptionType.MOBILE_NOT_FOUND, Severity = BillExceptionSeverity.Blocking, Description = "x" });
        var line = new BillLine { BillBatchId = batch.Id, MobileNumber = "7", RawText = "7" };
        db.BillLines.Add(line);
        db.MonthlyBills.Add(new MonthlyBill { EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = line.Id, EmployeeEpfSnapshot = "E", EmployeeNameSnapshot = "Employee", MobileNumberSnapshot = "7", CategoryCodeSnapshot = "CAT", DesignationCodeSnapshot = "DES", FactoryCodeSnapshot = "FAC", DepartmentCodeSnapshot = "DEP", EntitlementEffectiveFromSnapshot = new DateOnly(2026, 9, 1) });
        await db.SaveChangesAsync();
        var service = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), new TestClock());

        var refusal = await Assert.ThrowsAsync<BillWorkflowConflictException>(() => service.SubmitAsync(batch.Id, new WorkflowCommentRequest(null), default));

        Assert.Equal("1 number(s) in Exception Review are not assigned to an employee. Assign them before submitting for IT review. 1 number(s) have no responsibility assigned. Assign By User or By Company before submitting for IT review.", refusal.Message);
    }

    [Fact]
    public async Task Submit_is_refused_while_a_number_has_no_responsibility_assigned()
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db);
        var line = new BillLine { BillBatchId = batch.Id, MobileNumber = "7", RawText = "7" };
        db.BillLines.Add(line);
        var bill = new MonthlyBill { EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = line.Id, EmployeeEpfSnapshot = "E", EmployeeNameSnapshot = "Employee", MobileNumberSnapshot = "7", CategoryCodeSnapshot = "CAT", DesignationCodeSnapshot = "DES", FactoryCodeSnapshot = "FAC", DepartmentCodeSnapshot = "DEP", EntitlementEffectiveFromSnapshot = new DateOnly(2026, 9, 1), CalculatedExcess = 25m };
        db.MonthlyBills.Add(bill);
        // A number still open in Exception Review that already has its bill is not an unassigned number.
        db.BillExceptions.Add(new BillException { BillBatchId = batch.Id, BillLineId = line.Id, ExceptionType = BillExceptionType.ZERO_BILL, Severity = BillExceptionSeverity.Information, Description = "x" });
        await db.SaveChangesAsync();
        var service = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), new TestClock());

        var refusal = await Assert.ThrowsAsync<BillWorkflowConflictException>(() => service.SubmitAsync(batch.Id, new WorkflowCommentRequest(null), default));
        Assert.Contains("1 number(s) have no responsibility assigned", refusal.Message);

        bill.Responsibility = Responsibility.ByUser;
        bill.AssessedAt = DateTimeOffset.UnixEpoch;
        await db.SaveChangesAsync();
        await service.SubmitAsync(batch.Id, new WorkflowCommentRequest(null), default);
        Assert.Equal(BillBatchStatus.ITReview, (await db.BillBatches.SingleAsync()).Status);
    }

    [Fact]
    public async Task The_next_approver_is_notified_when_a_batch_reaches_IT_HR_and_Finance_approval_but_not_on_completion_or_return()
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db);
        var notifier = new RecordingNotifier(db);
        var service = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), new TestClock(), notifier);

        await service.SubmitAsync(batch.Id, new WorkflowCommentRequest("Ready"), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, "IT checked"), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.ReturnForCorrection, "Fix the totals"), default);
        await service.SubmitAsync(batch.Id, new WorkflowCommentRequest("Fixed"), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, "IT checked"), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, "HR approved"), default);
        await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, "Finance approved"), default);

        // The status each notification saw, read from the database: the step is saved before anyone is told.
        Assert.Equal([BillBatchStatus.ITReview, BillBatchStatus.HRApproval, BillBatchStatus.ITReview, BillBatchStatus.HRApproval, BillBatchStatus.FinanceApproval], notifier.Statuses);
    }

    [Theory]
    [InlineData(UserRole.HeadOfIt, "IT approval")]
    [InlineData(UserRole.GroupHrManager, "HR approval")]
    [InlineData(UserRole.Cfo, "Finance approval")]
    [InlineData(UserRole.Administrator, "Finance approval,IT approval,HR approval")]
    [InlineData(UserRole.ITEngineer, "")]
    [InlineData(UserRole.Viewer, "")]
    public async Task Pending_approvals_are_the_batches_the_users_role_can_approve_now_longest_waiting_first(UserRole role, string expectedSteps)
    {
        await using var db = CreateDb();
        var provider = new TelecomProvider { Code = "DIALOG", Name = "Dialog Axiata" };
        db.Add(provider);
        // Finance has waited longest, then IT, then HR; a Validated and a Completed batch wait for no approver.
        var waits = new[] { (BillBatchStatus.FinanceApproval, 8, 1), (BillBatchStatus.ITReview, 9, 3), (BillBatchStatus.HRApproval, 10, 5), (BillBatchStatus.Validated, 11, 6), (BillBatchStatus.Completed, 7, 7) };
        foreach (var (status, month, day) in waits)
        {
            var batch = new BillBatch { ProviderId = provider.Id, CorporateCode = "C", BillingYear = 2026, BillingMonth = month, Status = status };
            db.BillBatches.Add(batch);
            db.BillLines.Add(new BillLine { BillBatchId = batch.Id, MobileNumber = "1", RawText = "1" });
            db.ApprovalHistories.Add(new ApprovalHistory { BillBatchId = batch.Id, UserId = "u", DisplayName = "Earlier", ApprovedBy = "Earlier", ApprovedAt = new DateTimeOffset(2026, 10, day, 0, 0, 0, TimeSpan.Zero) });
            db.ApprovalHistories.Add(new ApprovalHistory { BillBatchId = batch.Id, UserId = "u", DisplayName = "Last Approver", ApprovedBy = "Last Approver", Comment = "Checked", ApprovedAt = new DateTimeOffset(2026, 11, day, 0, 0, 0, TimeSpan.Zero) });
        }
        await db.SaveChangesAsync();
        var user = new RoleUser(role);
        var service = new EfBillApprovalWorkflowService(db, new MobileBill.Api.Identity.RoleBasedBillReviewAuthorizationService(user), user, new TestClock());

        var pending = await service.GetPendingApprovalsAsync(default);

        Assert.Equal(expectedSteps, string.Join(",", pending.Select(x => x.Step)));
        Assert.All(pending, x => Assert.Equal(("Dialog Axiata", 1, "Last Approver", "Checked"), (x.Provider, x.NumberCount, x.LastActionBy, x.LastComment)));
        Assert.All(pending, x => Assert.Equal(11, x.WaitingSince!.Value.Month));
    }

    [Fact]
    public async Task Locking_a_batch_queues_its_report_emails_after_the_lock_is_saved()
    {
        await using var db = CreateDb();
        var batch = await AddBatchAsync(db);
        var reports = new RecordingLockDistribution(db);
        var service = new EfBillApprovalWorkflowService(db, new AllowWorkflowAuthorization(), new TestUser(), new TestClock(), reportDistribution: reports);
        await service.SubmitAsync(batch.Id, new WorkflowCommentRequest(null), default);
        foreach (var comment in new[] { "IT", "HR", "Finance" }) await service.DecideAsync(batch.Id, new WorkflowDecisionRequest(ApprovalAction.Approve, comment), default);
        Assert.Empty(reports.Statuses);

        await service.LockAsync(batch.Id, default);

        Assert.Equal([BillBatchStatus.Locked], reports.Statuses);
    }

    private sealed class RecordingLockDistribution(MobileBillDbContext db) : MobileBill.Application.Reports.IReportLockDistribution
    {
        public List<BillBatchStatus> Statuses { get; } = [];
        public async Task QueueOnLockAsync(Guid batchId, CancellationToken cancellationToken) =>
            Statuses.Add(await db.BillBatches.AsNoTracking().Where(x => x.Id == batchId).Select(x => x.Status).SingleAsync(cancellationToken));
    }

    private sealed class RoleUser(UserRole role) : ICurrentUserService { public string UserId => "u"; public string DisplayName => "User"; public UserRole Role => role; }

    private sealed class RecordingNotifier(MobileBillDbContext db) : MobileBill.Application.Notifications.IBillApprovalNotifier
    {
        public List<BillBatchStatus> Statuses { get; } = [];
        public async Task NotifyAwaitingApprovalAsync(Guid batchId, CancellationToken cancellationToken) =>
            Statuses.Add(await db.BillBatches.AsNoTracking().Where(x => x.Id == batchId).Select(x => x.Status).SingleAsync(cancellationToken));
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
