using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Billing;

public sealed class EfBillBatchReviewQueryService(MobileBillDbContext db) : IBillBatchReviewQueryService
{
    public async Task<BillBatchReviewSummaryDto> GetSummaryAsync(Guid batchId, CancellationToken token)
    {
        var batch = await db.BillBatches.AsNoTracking().Include(x => x.Provider).SingleOrDefaultAsync(x => x.Id == batchId, token) ?? throw new BillBatchNotFoundException(batchId);
        var lines = db.BillLines.AsNoTracking().Where(x => x.BillBatchId == batchId && x.ExtractionStatus == BillLineExtractionStatus.Extracted);
        var bills = db.MonthlyBills.AsNoTracking().Where(x => x.BillLine.BillBatchId == batchId);
        var exceptions = db.BillExceptions.AsNoTracking().Where(x => x.BillBatchId == batchId);
        var approvalHistory = await db.ApprovalHistories.AsNoTracking().Where(x => x.BillBatchId == batchId).OrderBy(x => x.ApprovedAt).ThenBy(x => x.Id)
            .Select(x => new ApprovalHistoryItemDto(x.Id, x.Stage, x.Action, x.WorkflowRole, x.UserId, x.DisplayName, x.ApprovedAt, x.Comment, x.PreviousStatus, x.NewStatus)).ToListAsync(token);
        var roles = await ResolveUserRolesAsync(approvalHistory.Select(x => x.UserId), token);
        approvalHistory = approvalHistory.Select(x => roles.TryGetValue(x.UserId, out var role) ? x with { UserRole = role } : x).ToList();
        return new BillBatchReviewSummaryDto(batch.Id, batch.BillingYear, batch.BillingMonth, batch.Provider.Name, batch.CorporateCode, batch.Status, batch.ValidationLevel,
            await lines.CountAsync(token), await lines.SumAsync(x => (decimal?)x.TotalDueAmount, token) ?? 0m,
            await bills.SumAsync(x => (decimal?)x.CalculatedExcess, token) ?? 0m, await bills.SumAsync(x => (decimal?)x.FinalDeduction, token) ?? 0m,
            await bills.Where(x => x.Responsibility == Responsibility.ByCompany).SumAsync(x => (decimal?)x.CalculatedExcess, token) ?? 0m,
            await exceptions.CountAsync(token), await exceptions.CountAsync(x => x.ExceptionType == BillExceptionType.MOBILE_NOT_FOUND && x.Status != BillExceptionStatus.Resolved, token),
            await bills.CountAsync(x => x.AssessedAt != null, token), await bills.CountAsync(x => x.AssessedAt == null, token))
        { ApprovalHistory = approvalHistory, UnresolvedExceptionCount = await exceptions.CountAsync(x => x.Status == BillExceptionStatus.Open || x.Status == BillExceptionStatus.InReview, token) };
    }

    public async Task<PagedResult<BillReviewRowDto>> GetRowsAsync(Guid batchId, BillReviewRowsRequest request, CancellationToken token)
    {
        if (!await db.BillBatches.AnyAsync(x => x.Id == batchId, token)) throw new BillBatchNotFoundException(batchId);
        var query = db.MonthlyBills.AsNoTracking().Where(x => x.BillLine.BillBatchId == batchId);
        if (!string.IsNullOrWhiteSpace(request.FactoryCode)) query = query.Where(x => x.FactoryCodeSnapshot == request.FactoryCode);
        if (!string.IsNullOrWhiteSpace(request.DepartmentCode)) query = query.Where(x => x.DepartmentCodeSnapshot == request.DepartmentCode);
        if (!string.IsNullOrWhiteSpace(request.CategoryCode)) query = query.Where(x => x.CategoryCodeSnapshot == request.CategoryCode);
        if (!string.IsNullOrWhiteSpace(request.SectionCode)) query = query.Where(x => x.SectionCodeSnapshot == request.SectionCode);
        if (request.Responsibility is not null) query = request.Responsibility switch { ReviewResponsibilityFilter.Unassessed => query.Where(x => x.Responsibility == null), ReviewResponsibilityFilter.ByUser => query.Where(x => x.Responsibility == Responsibility.ByUser), _ => query.Where(x => x.Responsibility == Responsibility.ByCompany) };
        if (request.Exception == ReviewExceptionFilter.HasException) query = query.Where(x => x.Exceptions.Any());
        if (request.Exception == ReviewExceptionFilter.NoException) query = query.Where(x => !x.Exceptions.Any());
        if (request.Status is not null) query = query.Where(x => x.Status == request.Status);
        if (request.CalculatedExcessMin is not null && request.CalculatedExcessMax is not null && request.CalculatedExcessMin > request.CalculatedExcessMax)
            throw new BillReviewValidationException("Calculated excess minimum cannot exceed maximum.");
        if (request.CalculatedExcessMin is not null) query = query.Where(x => x.CalculatedExcess >= request.CalculatedExcessMin.Value);
        if (request.CalculatedExcessMax is not null) query = query.Where(x => x.CalculatedExcess <= request.CalculatedExcessMax.Value);
        var search = request.Search?.Trim(); if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.MobileNumberSnapshot.Contains(search) || x.EmployeeEpfSnapshot.Contains(search) || x.EmployeeNameSnapshot.Contains(search) || (x.CallingNameSnapshot != null && x.CallingNameSnapshot.Contains(search)));
        query = Sort(query, request.SortBy, request.SortDirection);
        var count = await query.CountAsync(token); var page = request.NormalizedPageNumber; var size = request.NormalizedPageSize;
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => new BillReviewRowDto(x.Id, x.MobileNumberSnapshot, x.EmployeeEpfSnapshot, x.EmployeeNameSnapshot, x.CallingNameSnapshot, x.CategoryNameSnapshot ?? "", x.DesignationNameSnapshot ?? "", x.FactoryNameSnapshot ?? "", x.DepartmentNameSnapshot ?? "", x.CreditLimit, x.MonthlyRental, x.CreditLimit + x.MonthlyRental, x.ActualBill, x.Variance, x.CalculatedExcess, x.Responsibility, x.FinalDeduction, x.Remark, x.Status, x.Exceptions.Any(), x.AssessedAt != null, x.SectionNameSnapshot, x.SubSectionNameSnapshot, x.IsPooled)).ToListAsync(token);
        return new PagedResult<BillReviewRowDto>(items, page, size, count);
    }

    public async Task<BillReviewDetailDto> GetDetailAsync(Guid batchId, Guid monthlyBillId, CancellationToken token)
    {
        var detail = await db.MonthlyBills.AsNoTracking().Include(x => x.BillLine).SingleOrDefaultAsync(x => x.Id == monthlyBillId && x.BillLine.BillBatchId == batchId, token) ?? throw new BillReviewRowNotFoundException(monthlyBillId);
        var lineExceptions = await db.BillExceptions.AsNoTracking().Where(x => x.BillLineId == detail.BillLineId).ToListAsync(token);
        var exceptionIds = lineExceptions.Select(x => x.Id).ToList();
        var approvals = await db.ApprovalHistories.AsNoTracking().Where(x => x.BillBatchId == batchId).OrderBy(x => x.ApprovedAt).Select(x => $"{x.WorkflowRole}: {x.Action} by {x.DisplayName}").ToListAsync(token);
        var auditRows = await db.AuditLogs.AsNoTracking().Where(x => (x.EntityName == nameof(MonthlyBill) && x.EntityId == monthlyBillId) || (x.EntityName == nameof(BillException) && exceptionIds.Contains(x.EntityId))).OrderByDescending(x => x.PerformedAt).Select(x => new { x.Action, x.PerformedBy, x.PerformedAt }).ToListAsync(token);
        var names = await ResolveUserNamesAsync(auditRows.Select(x => x.PerformedBy).Append(detail.AssessedBy), token);
        var audit = auditRows.Select(x => $"{x.Action} by {DisplayName(names, x.PerformedBy)} at {x.PerformedAt:O}").ToList();
        var l = detail.BillLine;
        return new BillReviewDetailDto(Row(detail, lineExceptions.Count != 0),
            new ChargeBreakdownDto(l.PreviousDueAmount, l.Payments, l.TotalUsageCharges, l.Idd, l.Roaming, l.ValueAddedServices, l.Discounts, l.BillAdjustmentsBalanceTransfers, l.CommitmentCharges, l.LatePaymentCharges, l.AddToBill, l.InstalmentPlans, l.GovernmentTaxesAndLevies, l.Vat, l.ChargesForBillPeriod, l.TotalDueAmount), lineExceptions.Select(x => x.ExceptionType.ToString()).ToList(), approvals, audit, detail.AssessedBy is null ? null : DisplayName(names, detail.AssessedBy), detail.AssessedAt, detail.DeductionOverrideReason);
    }

    private async Task<Dictionary<string, string>> ResolveUserNamesAsync(IEnumerable<string?> userIds, CancellationToken token)
    {
        var ids = userIds.Select(x => Guid.TryParse(x, out var id) ? id : (Guid?)null).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return new(StringComparer.OrdinalIgnoreCase);
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.DisplayName }).ToListAsync(token);
        return users.ToDictionary(x => x.Id.ToString(), x => x.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    // The user's current account role; approval history does not snapshot it.
    private async Task<Dictionary<string, string>> ResolveUserRolesAsync(IEnumerable<string?> userIds, CancellationToken token)
    {
        var ids = userIds.Select(x => Guid.TryParse(x, out var id) ? id : (Guid?)null).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return new(StringComparer.OrdinalIgnoreCase);
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Role }).ToListAsync(token);
        return users.ToDictionary(x => x.Id.ToString(), x => x.Role.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private static string DisplayName(Dictionary<string, string> names, string userId) => names.TryGetValue(userId, out var name) ? name : userId;

    private static IQueryable<MonthlyBill> Sort(IQueryable<MonthlyBill> q, string? key, string? direction)
    { var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase); return (key?.ToLowerInvariant()) switch { "epf" => desc ? q.OrderByDescending(x => x.EmployeeEpfSnapshot) : q.OrderBy(x => x.EmployeeEpfSnapshot), "employeename" => desc ? q.OrderByDescending(x => x.EmployeeNameSnapshot) : q.OrderBy(x => x.EmployeeNameSnapshot), "callingname" => desc ? q.OrderByDescending(x => x.CallingNameSnapshot) : q.OrderBy(x => x.CallingNameSnapshot), "designation" => desc ? q.OrderByDescending(x => x.DesignationNameSnapshot) : q.OrderBy(x => x.DesignationNameSnapshot), "factory" => desc ? q.OrderByDescending(x => x.FactoryNameSnapshot) : q.OrderBy(x => x.FactoryNameSnapshot), "department" => desc ? q.OrderByDescending(x => x.DepartmentNameSnapshot) : q.OrderBy(x => x.DepartmentNameSnapshot), "section" => desc ? q.OrderByDescending(x => x.SectionNameSnapshot) : q.OrderBy(x => x.SectionNameSnapshot), "subsection" => desc ? q.OrderByDescending(x => x.SubSectionNameSnapshot) : q.OrderBy(x => x.SubSectionNameSnapshot), "category" => desc ? q.OrderByDescending(x => x.CategoryNameSnapshot) : q.OrderBy(x => x.CategoryNameSnapshot), "creditlimit" => desc ? q.OrderByDescending(x => x.CreditLimit) : q.OrderBy(x => x.CreditLimit), "monthlyrental" => desc ? q.OrderByDescending(x => x.MonthlyRental) : q.OrderBy(x => x.MonthlyRental), "actualbill" => desc ? q.OrderByDescending(x => x.ActualBill) : q.OrderBy(x => x.ActualBill), "variance" => desc ? q.OrderByDescending(x => x.Variance) : q.OrderBy(x => x.Variance), "calculatedexcess" => desc ? q.OrderByDescending(x => x.CalculatedExcess) : q.OrderBy(x => x.CalculatedExcess), "finaldeduction" => desc ? q.OrderByDescending(x => x.FinalDeduction) : q.OrderBy(x => x.FinalDeduction), "responsibility" => desc ? q.OrderByDescending(x => x.Responsibility) : q.OrderBy(x => x.Responsibility), "status" => desc ? q.OrderByDescending(x => x.Status) : q.OrderBy(x => x.Status), _ => desc ? q.OrderByDescending(x => x.MobileNumberSnapshot) : q.OrderBy(x => x.MobileNumberSnapshot) }; }
    private static BillReviewRowDto Row(MonthlyBill x, bool hasException) => new(x.Id, x.MobileNumberSnapshot, x.EmployeeEpfSnapshot, x.EmployeeNameSnapshot, x.CallingNameSnapshot, x.CategoryNameSnapshot ?? "", x.DesignationNameSnapshot ?? "", x.FactoryNameSnapshot ?? "", x.DepartmentNameSnapshot ?? "", x.CreditLimit, x.MonthlyRental, x.CreditLimit + x.MonthlyRental, x.ActualBill, x.Variance, x.CalculatedExcess, x.Responsibility, x.FinalDeduction, x.Remark, x.Status, hasException, x.AssessedAt != null, x.SectionNameSnapshot, x.SubSectionNameSnapshot, x.IsPooled);
}
