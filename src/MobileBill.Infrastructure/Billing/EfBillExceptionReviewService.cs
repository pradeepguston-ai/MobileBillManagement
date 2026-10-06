using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Billing;

public sealed class EfBillExceptionReviewService(MobileBillDbContext db, IBillReviewAuthorizationService authorization, ICurrentUserService user, IClock clock) : IBillExceptionReviewService
{
    public async Task<PagedResult<BillExceptionDto>> GetExceptionsAsync(Guid batchId, BillExceptionListRequest request, CancellationToken token)
    {
        if (!await db.BillBatches.AnyAsync(x => x.Id == batchId, token)) throw new BillBatchNotFoundException(batchId);
        var search = request.Search?.Trim();
        var query = db.BillExceptions.AsNoTracking().Include(x => x.BillLine).Include(x => x.MonthlyBill)
            .Where(x => x.BillBatchId == batchId && (string.IsNullOrWhiteSpace(search) || x.Description.Contains(search) || (x.BillLine != null && x.BillLine.MobileNumber.Contains(search))));
        if (request.ExceptionType is not null) query = query.Where(x => x.ExceptionType == request.ExceptionType);
        if (request.Resolution == BillExceptionResolutionFilter.Unresolved) query = query.Where(x => x.Status == BillExceptionStatus.Open || x.Status == BillExceptionStatus.InReview);
        if (request.Resolution == BillExceptionResolutionFilter.Resolved) query = query.Where(x => x.Status == BillExceptionStatus.Resolved || x.Status == BillExceptionStatus.Waived);
        var total = await query.CountAsync(token); var page = request.NormalizedPageNumber; var size = request.NormalizedPageSize;
        var items = await query.OrderBy(x => x.Status).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).Select(x => new BillExceptionDto(x.Id, x.BillBatchId, x.BillLineId, x.BillLine == null ? null : x.BillLine.MobileNumber, x.ExceptionType, x.Severity, x.Status, x.Description, x.Resolution, x.ResolvedBy, x.ResolvedAt, x.MonthlyBill == null ? null : x.MonthlyBill.AllocationMatchMethod, x.MonthlyBill == null ? null : x.MonthlyBill.EntitlementMatchMethod)).ToListAsync(token);
        return new PagedResult<BillExceptionDto>(items, page, size, total);
    }

    public async Task<BillExceptionSummaryDto> GetSummaryAsync(Guid batchId, CancellationToken token)
    {
        if (!await db.BillBatches.AnyAsync(x => x.Id == batchId, token)) throw new BillBatchNotFoundException(batchId);
        var query = db.BillExceptions.AsNoTracking().Where(x => x.BillBatchId == batchId);
        var total = await query.CountAsync(token);
        var unresolved = await query.CountAsync(x => x.Status == BillExceptionStatus.Open || x.Status == BillExceptionStatus.InReview, token);
        return new BillExceptionSummaryDto(batchId, total, unresolved, total - unresolved);
    }

    public async Task<IReadOnlyList<MobileAccountCandidateDto>> GetMobileAccountCandidatesAsync(Guid exceptionId, CancellationToken token)
    {
        var exception = await GetExceptionWithLine(exceptionId, token);
        if (exception.ExceptionType != BillExceptionType.MOBILE_NOT_FOUND) throw new BillReviewConflictException("Historical allocation candidates are available only for MOBILE_NOT_FOUND exceptions.");
        return await db.MobileAccounts.AsNoTracking().Include(x => x.Employee).Where(x => x.MobileNumber == exception.BillLine!.MobileNumber)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.Id).Select(x => new MobileAccountCandidateDto(x.Id, x.Employee.Id, x.MobileNumber, x.Employee.EPF, x.Employee.FullName, x.IsActive)).ToListAsync(token);
    }

    public async Task<BillExceptionResolutionResult> ResolveMobileNotFoundAsync(Guid exceptionId, ResolveMobileAccountExceptionRequest request, CancellationToken token)
    {
        if (!await authorization.CanResolveExceptionsAsync(token)) throw new BillReviewForbiddenException();
        if (request.MobileAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Trim().Length < 5)
            throw new BillReviewValidationException("A meaningful resolution comment and mobile account are required.");
        var exception = await GetExceptionWithLine(exceptionId, token);
        if (exception.ExceptionType != BillExceptionType.MOBILE_NOT_FOUND || exception.Status != BillExceptionStatus.Open)
            throw new BillReviewConflictException("Only an open MOBILE_NOT_FOUND exception can be resolved by historical allocation override.");
        var line = exception.BillLine!;
        var allocation = await db.MobileAccounts.Include(x => x.Employee).ThenInclude(x => x.Category)
            .Include(x => x.Employee).ThenInclude(x => x.Designation)
            .Include(x => x.Employee).ThenInclude(x => x.Factory)
            .Include(x => x.Employee).ThenInclude(x => x.Department)
            .Include(x => x.Employee).ThenInclude(x => x.Section)
            .Include(x => x.Employee).ThenInclude(x => x.SubSection)
            .SingleOrDefaultAsync(x => x.Id == request.MobileAccountId && x.MobileNumber == line.MobileNumber, token)
            ?? throw new BillReviewValidationException("The selected allocation does not belong to the bill line mobile number.");
        var batch = await db.BillBatches.SingleAsync(x => x.Id == exception.BillBatchId, token);
        if (batch.Status == BillBatchStatus.Locked) throw new BillReviewConflictException("Locked bill batches cannot have exception resolutions changed.");

        var now = clock.UtcNow;
        var resolution = new BillExceptionResolution { BillExceptionId = exception.Id, BillLineId = line.Id, MobileAccountId = allocation.Id, EmployeeId = allocation.Employee.Id, MobileNumber = line.MobileNumber, EmployeeEpf = allocation.Employee.EPF, EmployeeName = allocation.Employee.FullName, OriginalExceptionType = exception.ExceptionType, BillingYear = batch.BillingYear, BillingMonth = batch.BillingMonth, ResolutionComment = request.Comment.Trim(), ResolvedBy = user.UserId, ResolvedAt = now, CreatedAtUtc = now, CreatedBy = user.UserId };
        exception.Status = BillExceptionStatus.Resolved; exception.Resolution = request.Comment.Trim(); exception.ResolvedBy = user.UserId; exception.ResolvedAt = now; exception.UpdatedAtUtc = now; exception.UpdatedBy = user.UserId;
        db.BillExceptionResolutions.Add(resolution);
        db.AuditLogs.Add(new AuditLog { EntityName = nameof(BillException), EntityId = exception.Id, Action = "ManualHistoricalMobileAccountOverride", BeforeDataJson = JsonSerializer.Serialize(new { exception.Status, ExceptionType = exception.ExceptionType }), AfterDataJson = JsonSerializer.Serialize(new { allocation.Id, EmployeeId = allocation.Employee.Id, Comment = request.Comment.Trim() }), PerformedBy = user.UserId, PerformedAt = now, CreatedAtUtc = now, CreatedBy = user.UserId });

        if (!allocation.Employee.IsActive)
        {
            db.BillExceptions.Add(EfBillMatchingService.Exception(batch.Id, line.Id, BillExceptionType.EMPLOYEE_NOT_ACTIVE, BillExceptionSeverity.Blocking, "The manually selected allocation belongs to an inactive employee."));
            await db.SaveChangesAsync(token); return new BillExceptionResolutionResult(exception.Id, exception.Status, null, BillExceptionType.EMPLOYEE_NOT_ACTIVE);
        }
        var monthlyBill = EfBillMatchingService.CreateMonthlyBill(line, allocation, AllocationMatchMethod.ManualHistoricalOverride, now, user.UserId);
        db.MonthlyBills.Add(monthlyBill);
        if (EfBillMatchingService.AutoAssessmentAudit(monthlyBill, now, user.UserId) is { } autoAudit) db.AuditLogs.Add(autoAudit);
        await db.SaveChangesAsync(token);
        return new BillExceptionResolutionResult(exception.Id, exception.Status, monthlyBill.Id, null);
    }

    private async Task<BillException> GetExceptionWithLine(Guid id, CancellationToken token) => await db.BillExceptions.Include(x => x.BillLine).SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new BillReviewNotFoundException(id);
}
