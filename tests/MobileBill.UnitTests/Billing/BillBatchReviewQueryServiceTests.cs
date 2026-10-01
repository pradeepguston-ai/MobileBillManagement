using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Billing;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Billing;

public sealed class BillBatchReviewQueryServiceTests
{
    [Fact]
    public async Task Summary_returns_full_batch_kpis_and_preserves_unassessed_state()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        db.BillExceptions.Add(new BillException { BillBatchId = seed.Batch.Id, BillLineId = seed.Line.Id, ExceptionType = BillExceptionType.MOBILE_NOT_FOUND, Severity = BillExceptionSeverity.Blocking, Description = "Missing allocation" });
        await db.SaveChangesAsync();

        var result = await new EfBillBatchReviewQueryService(db).GetSummaryAsync(seed.Batch.Id, default);

        Assert.Equal(1, result.TotalAccounts);
        Assert.Equal(125m, result.TotalActualBill);
        Assert.Equal(1, result.ExceptionCount);
        Assert.Equal(1, result.UnmatchedCount);
        Assert.Equal(0, result.AssessedCount);
        Assert.Equal(1, result.UnassessedCount);
    }

    [Fact]
    public async Task Summary_projects_real_approval_history_in_chronological_order()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        db.ApprovalHistories.AddRange(
            Approval(seed.Batch.Id, ApprovalStage.HRApproval, WorkflowRole.HRApprover, ApprovalAction.Approve, "HR User", "hr-user", new DateTimeOffset(2026, 9, 10, 5, 0, 0, TimeSpan.Zero), "HR approved", BillBatchStatus.HRApproval, BillBatchStatus.FinanceApproval),
            Approval(seed.Batch.Id, ApprovalStage.ITReview, WorkflowRole.ITReviewer, ApprovalAction.Approve, "IT User", "it-user", new DateTimeOffset(2026, 9, 10, 4, 0, 0, TimeSpan.Zero), null, BillBatchStatus.ITReview, BillBatchStatus.HRApproval));
        await db.SaveChangesAsync();

        var result = await new EfBillBatchReviewQueryService(db).GetSummaryAsync(seed.Batch.Id, default);
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var history = json.GetProperty("approvalHistory").EnumerateArray().ToArray();

        Assert.Equal(2, history.Length);
        Assert.Equal("ITReview", history[0].GetProperty("workflowStage").GetString());
        Assert.Equal("ITReviewer", history[0].GetProperty("workflowRole").GetString());
        Assert.Equal("it-user", history[0].GetProperty("userId").GetString());
        Assert.Equal("IT User", history[0].GetProperty("displayName").GetString());
        Assert.Equal("Approve", history[0].GetProperty("action").GetString());
        Assert.Equal("ITReview", history[0].GetProperty("previousStatus").GetString());
        Assert.Equal("HRApproval", history[0].GetProperty("newStatus").GetString());
        Assert.Equal("HR approved", history[1].GetProperty("comment").GetString());
    }

    [Fact]
    public async Task Summary_includes_the_approving_users_role_when_the_user_exists()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var user = new User { Email = "hr@test.local", DisplayName = "HR Manager", PasswordHash = "x", Role = UserRole.GroupHrManager };
        db.Users.Add(user);
        db.ApprovalHistories.AddRange(
            Approval(seed.Batch.Id, ApprovalStage.HRApproval, WorkflowRole.HRApprover, ApprovalAction.Approve, "HR Manager", user.Id.ToString(), new DateTimeOffset(2026, 9, 10, 5, 0, 0, TimeSpan.Zero), null, BillBatchStatus.HRApproval, BillBatchStatus.FinanceApproval),
            Approval(seed.Batch.Id, ApprovalStage.ITReview, WorkflowRole.ITReviewer, ApprovalAction.Approve, "IT User", "unknown-user", new DateTimeOffset(2026, 9, 10, 4, 0, 0, TimeSpan.Zero), null, BillBatchStatus.ITReview, BillBatchStatus.HRApproval));
        await db.SaveChangesAsync();

        var result = await new EfBillBatchReviewQueryService(db).GetSummaryAsync(seed.Batch.Id, default);

        Assert.Null(result.ApprovalHistory[0].UserRole);
        Assert.Equal("GroupHrManager", result.ApprovalHistory[1].UserRole);
    }

    [Fact]
    public async Task Summary_returns_empty_approval_history_when_no_actions_exist()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);

        var result = await new EfBillBatchReviewQueryService(db).GetSummaryAsync(seed.Batch.Id, default);
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Empty(json.GetProperty("approvalHistory").EnumerateArray());
    }

    [Fact]
    public async Task Rows_filter_by_unassessed_responsibility_and_trimmed_search()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);

        var result = await new EfBillBatchReviewQueryService(db).GetRowsAsync(seed.Batch.Id, new BillReviewRowsRequest(Responsibility: ReviewResponsibilityFilter.Unassessed, Search: "  EPF-1  ", SortBy: "unsupported"), default);

        var row = Assert.Single(result.Items);
        Assert.Equal(seed.MonthlyBill.Id, row.Id);
        Assert.Equal("Unassessed", row.Responsibility?.ToString() ?? "Unassessed");
        Assert.Equal(125m, row.AvailableEntitlement);
    }

    [Fact]
    public async Task Rows_filter_by_calculated_excess_range()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var highLine = new BillLine { BillBatchId = seed.Batch.Id, BillBatch = seed.Batch, MobileNumber = "761499199", PageNumber = 1, RawText = "row", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 500m };
        var highBill = new MonthlyBill
        {
            EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = highLine.Id, BillLine = highLine,
            EmployeeEpfSnapshot = "EPF-2", EmployeeNameSnapshot = "Employee Two", MobileNumberSnapshot = highLine.MobileNumber,
            CategoryCodeSnapshot = "CAT", CategoryNameSnapshot = "Category", DesignationCodeSnapshot = "DES", DesignationNameSnapshot = "Designation", FactoryCodeSnapshot = "FAC", FactoryNameSnapshot = "Factory", DepartmentCodeSnapshot = "DEP", DepartmentNameSnapshot = "Department",
            EntitlementEffectiveFromSnapshot = new DateOnly(2026, 8, 1),
            CreditLimit = 100m, MonthlyRental = 25m, ActualBill = 500m, Variance = -375m, CalculatedExcess = 375m
        };
        db.AddRange(highLine, highBill);
        await db.SaveChangesAsync();

        var minOnly = await new EfBillBatchReviewQueryService(db).GetRowsAsync(seed.Batch.Id, new BillReviewRowsRequest(CalculatedExcessMin: 100m), default);
        var maxOnly = await new EfBillBatchReviewQueryService(db).GetRowsAsync(seed.Batch.Id, new BillReviewRowsRequest(CalculatedExcessMax: 100m), default);
        var range = await new EfBillBatchReviewQueryService(db).GetRowsAsync(seed.Batch.Id, new BillReviewRowsRequest(CalculatedExcessMin: 300m, CalculatedExcessMax: 400m), default);

        Assert.Equal(highBill.Id, Assert.Single(minOnly.Items).Id);
        Assert.Equal(seed.MonthlyBill.Id, Assert.Single(maxOnly.Items).Id);
        Assert.Equal(highBill.Id, Assert.Single(range.Items).Id);
    }

    [Fact]
    public async Task Rows_reject_a_calculated_excess_range_where_min_exceeds_max()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);

        await Assert.ThrowsAsync<BillReviewValidationException>(() => new EfBillBatchReviewQueryService(db).GetRowsAsync(seed.Batch.Id, new BillReviewRowsRequest(CalculatedExcessMin: 500m, CalculatedExcessMax: 100m), default));
    }

    [Fact]
    public async Task Detail_rejects_a_monthly_bill_that_belongs_to_a_different_batch()
    {
        await using var db = CreateDb();
        var first = await SeedAsync(db);
        var second = await SeedAsync(db);

        await Assert.ThrowsAsync<BillReviewRowNotFoundException>(() => new EfBillBatchReviewQueryService(db).GetDetailAsync(first.Batch.Id, second.MonthlyBill.Id, default));
    }

    [Fact]
    public async Task Detail_includes_bill_line_exceptions_and_their_audit_history()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var exception = new BillException
        {
            BillBatchId = seed.Batch.Id,
            BillLineId = seed.Line.Id,
            ExceptionType = BillExceptionType.MOBILE_NOT_FOUND,
            Severity = BillExceptionSeverity.Warning,
            Description = "The account needs review."
        };
        db.BillExceptions.Add(exception);
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(BillException),
            EntityId = exception.Id,
            Action = "Historical allocation override",
            PerformedBy = "dev-user",
            PerformedAt = new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        var result = await new EfBillBatchReviewQueryService(db).GetDetailAsync(seed.Batch.Id, seed.MonthlyBill.Id, default);

        Assert.Contains(nameof(BillExceptionType.MOBILE_NOT_FOUND), result.Exceptions);
        Assert.Contains(result.AuditHistory, value => value.Contains("Historical allocation override", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Detail_shows_user_display_names_instead_of_user_ids_in_audit_history_and_assessed_by()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var user = new User { Email = "reviewer@test.local", DisplayName = "Nimal Perera", PasswordHash = "x", Role = UserRole.ITEngineer };
        db.Users.Add(user);
        seed.MonthlyBill.AssessedBy = user.Id.ToString();
        db.AuditLogs.AddRange(
            new AuditLog { EntityName = nameof(MonthlyBill), EntityId = seed.MonthlyBill.Id, Action = "Assessed", PerformedBy = user.Id.ToString(), PerformedAt = new DateTimeOffset(2026, 9, 25, 4, 46, 14, TimeSpan.Zero) },
            new AuditLog { EntityName = nameof(MonthlyBill), EntityId = seed.MonthlyBill.Id, Action = "Matched", PerformedBy = "dev-user", PerformedAt = new DateTimeOffset(2026, 9, 25, 4, 0, 0, TimeSpan.Zero) });
        await db.SaveChangesAsync();

        var result = await new EfBillBatchReviewQueryService(db).GetDetailAsync(seed.Batch.Id, seed.MonthlyBill.Id, default);

        Assert.Contains(result.AuditHistory, value => value.StartsWith("Assessed by Nimal Perera at ", StringComparison.Ordinal));
        Assert.Contains(result.AuditHistory, value => value.StartsWith("Matched by dev-user at ", StringComparison.Ordinal));
        Assert.DoesNotContain(result.AuditHistory, value => value.Contains(user.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Nimal Perera", result.AssessedBy);
    }

    [Fact]
    public async Task Detail_returns_existing_assessment_metadata_and_transaction_snapshots()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        seed.MonthlyBill.AssessedBy = "reviewer-1";
        seed.MonthlyBill.AssessedAt = new DateTimeOffset(2026, 9, 9, 4, 30, 0, TimeSpan.Zero);
        seed.MonthlyBill.DeductionOverrideReason = "Approved waiver";
        seed.MonthlyBill.EmployeeNameSnapshot = "Historical Employee Name";
        await db.SaveChangesAsync();

        var result = await new EfBillBatchReviewQueryService(db).GetDetailAsync(seed.Batch.Id, seed.MonthlyBill.Id, default);
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("Historical Employee Name", result.Row.EmployeeName);
        Assert.Equal("reviewer-1", json.GetProperty("assessedBy").GetString());
        Assert.Equal(new DateTimeOffset(2026, 9, 9, 4, 30, 0, TimeSpan.Zero), json.GetProperty("assessedAt").GetDateTimeOffset());
        Assert.Equal("Approved waiver", json.GetProperty("deductionOverrideReason").GetString());
    }

    [Fact]
    public async Task Rows_keep_one_row_per_monthly_bill_when_multiple_exceptions_exist()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        db.BillExceptions.AddRange(
            new BillException { BillBatchId = seed.Batch.Id, BillLineId = seed.Line.Id, MonthlyBillId = seed.MonthlyBill.Id, ExceptionType = BillExceptionType.ZERO_BILL, Severity = BillExceptionSeverity.Warning, Description = "Zero" },
            new BillException { BillBatchId = seed.Batch.Id, BillLineId = seed.Line.Id, MonthlyBillId = seed.MonthlyBill.Id, ExceptionType = BillExceptionType.PARSER_WARNING, Severity = BillExceptionSeverity.Warning, Description = "Parser" });
        await db.SaveChangesAsync();

        var result = await new EfBillBatchReviewQueryService(db).GetRowsAsync(seed.Batch.Id, new BillReviewRowsRequest(Exception: ReviewExceptionFilter.HasException), default);

        Assert.Single(result.Items);
        Assert.True(result.Items[0].HasException);
    }

    private static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Seed> SeedAsync(MobileBillDbContext db)
    {
        var provider = new TelecomProvider { Code = $"TEL-{Guid.NewGuid():N}"[..12], Name = "Telecom" };
        var batch = new BillBatch { ProviderId = provider.Id, Provider = provider, CorporateCode = "CORP", BillingYear = 2026, BillingMonth = 8, Status = BillBatchStatus.Validated };
        var line = new BillLine { BillBatchId = batch.Id, BillBatch = batch, MobileNumber = "768791861", PageNumber = 1, RawText = "row", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 125m };
        var monthlyBill = new MonthlyBill
        {
            EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = line.Id, BillLine = line,
            EmployeeEpfSnapshot = "EPF-1", EmployeeNameSnapshot = "Employee One", MobileNumberSnapshot = line.MobileNumber,
            CategoryCodeSnapshot = "CAT", CategoryNameSnapshot = "Category", DesignationCodeSnapshot = "DES", DesignationNameSnapshot = "Designation", FactoryCodeSnapshot = "FAC", FactoryNameSnapshot = "Factory", DepartmentCodeSnapshot = "DEP", DepartmentNameSnapshot = "Department",
            EntitlementEffectiveFromSnapshot = new DateOnly(2026, 8, 1),
            CreditLimit = 100m, MonthlyRental = 25m, ActualBill = 125m, Variance = 0m, CalculatedExcess = 0m
        };
        db.AddRange(provider, batch, line, monthlyBill);
        await db.SaveChangesAsync();
        return new Seed(batch, line, monthlyBill);
    }

    private sealed record Seed(BillBatch Batch, BillLine Line, MonthlyBill MonthlyBill);

    private static ApprovalHistory Approval(Guid batchId, ApprovalStage stage, WorkflowRole role, ApprovalAction action, string displayName, string userId, DateTimeOffset at, string? comment, BillBatchStatus previous, BillBatchStatus next) => new()
    {
        BillBatchId = batchId, Stage = stage, WorkflowRole = role, Action = action, Decision = ApprovalDecision.Approved,
        DisplayName = displayName, UserId = userId, ApprovedBy = displayName, ApprovedAt = at, Comment = comment,
        PreviousStatus = previous, NewStatus = next
    };
}
