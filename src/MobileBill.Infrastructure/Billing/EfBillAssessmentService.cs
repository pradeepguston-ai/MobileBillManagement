using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Calculations;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Billing;

public sealed class EfBillAssessmentService(MobileBillDbContext db, IBillReviewAuthorizationService authorization, ICurrentUserService currentUser, IClock clock) : IBillAssessmentService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
    public async Task<MonthlyBillAssessmentDto> AssessAsync(Guid monthlyBillId, AssessMonthlyBillRequest request, CancellationToken token)
    {
        if (!await authorization.CanResolveExceptionsAsync(token)) throw new BillReviewForbiddenException();
        return await AssessCoreAsync(monthlyBillId, request, token);
    }

    public async Task<BulkAssessmentResultDto> BulkAssessAsync(BulkAssessMonthlyBillsRequest request, CancellationToken token)
    {
        if (!await authorization.CanResolveExceptionsAsync(token)) throw new BillReviewForbiddenException();
        if (request.MonthlyBillIds.Count == 0) throw new BillAssessmentValidationException("Select at least one monthly bill to assess.");
        var items = new List<BulkAssessmentItemResult>();
        foreach (var monthlyBillId in request.MonthlyBillIds)
        {
            try
            {
                await AssessCoreAsync(monthlyBillId, new AssessMonthlyBillRequest(request.Responsibility, null, request.Reason), token);
                items.Add(new BulkAssessmentItemResult(monthlyBillId, true, null));
            }
            catch (Exception ex) when (ex is BillAssessmentNotFoundException or BillAssessmentValidationException or BillReviewConflictException)
            {
                items.Add(new BulkAssessmentItemResult(monthlyBillId, false, ex.Message));
            }
        }
        return new BulkAssessmentResultDto(items.Count(item => item.Success), items.Count(item => !item.Success), items);
    }

    private async Task<MonthlyBillAssessmentDto> AssessCoreAsync(Guid monthlyBillId, AssessMonthlyBillRequest request, CancellationToken token)
    {
        var bill = await db.MonthlyBills.Include(x => x.BillLine).SingleOrDefaultAsync(x => x.Id == monthlyBillId, token)
            ?? throw new BillAssessmentNotFoundException(monthlyBillId);
        if (await db.BillBatches.AnyAsync(x => x.Id == bill.BillLine.BillBatchId && x.Status == BillBatchStatus.Locked, token)) throw new BillReviewConflictException("Locked bill batches cannot be assessed or reassessed.");
        var isReassessment = bill.AssessedAt is not null;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (isReassessment && string.IsNullOrWhiteSpace(reason))
            throw new BillAssessmentValidationException("A reason is required when changing an already assessed monthly bill.");

        var calculation = MonthlyBillCalculation.Create(bill.BillLine.TotalDueAmount, bill.CreditLimit, bill.MonthlyRental);
        var finalDeduction = ResolveFinalDeduction(request, calculation.CalculatedExcess, isReassessment, reason, out var isOverride);
        var previous = new { bill.Responsibility, bill.FinalDeduction, bill.AssessedAt, bill.AssessedBy };
        var now = clock.UtcNow;

        bill.ActualBill = calculation.ActualBill;
        bill.Variance = calculation.Variance;
        bill.CalculatedExcess = calculation.CalculatedExcess;
        bill.Responsibility = request.Responsibility;
        bill.FinalDeduction = finalDeduction;
        bill.AssessedAt = now;
        bill.AssessedBy = currentUser.UserId;
        // The reason typed in the assessment is the bill's remark on screen and in the reports; a blank
        // reason leaves an earlier remark in place.
        if (reason is not null) bill.Remark = reason;
        bill.UpdatedAtUtc = now;
        bill.UpdatedBy = currentUser.UserId;
        if (isOverride)
        {
            bill.DeductionOverrideAmount = finalDeduction;
            bill.DeductionOverrideReason = reason;
            bill.DeductionOverrideBy = currentUser.UserId;
            bill.DeductionOverrideAt = now;
        }
        else
        {
            bill.DeductionOverrideAmount = null;
            bill.DeductionOverrideReason = null;
            bill.DeductionOverrideBy = null;
            bill.DeductionOverrideAt = null;
        }
        var employee = await db.Employees.FindAsync([bill.EmployeeId], token);
        if (employee is not null) employee.DefaultResponsibility = request.Responsibility;
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(MonthlyBill), EntityId = bill.Id, Action = isReassessment ? "Reassessed" : "Assessed",
            BeforeDataJson = JsonSerializer.Serialize(previous, AuditJsonOptions),
            AfterDataJson = JsonSerializer.Serialize(new { bill.Responsibility, bill.FinalDeduction, Reason = reason, bill.AssessedAt, bill.AssessedBy }, AuditJsonOptions),
            PerformedBy = currentUser.UserId, PerformedAt = now, CreatedAtUtc = now, CreatedBy = currentUser.UserId
        });
        await db.SaveChangesAsync(token);
        return Map(bill, calculation.AvailableEntitlement);
    }

    private static decimal ResolveFinalDeduction(AssessMonthlyBillRequest request, decimal calculatedExcess, bool isReassessment, string? reason, out bool isOverride)
    {
        if (request.Responsibility == Responsibility.ByCompany)
        {
            if (request.FinalDeduction is not null and not 0m) throw new BillAssessmentValidationException("ByCompany responsibility always has a final deduction of 0.00.");
            isOverride = false;
            return 0m;
        }
        var finalDeduction = request.FinalDeduction ?? calculatedExcess;
        if (finalDeduction < 0m || finalDeduction > calculatedExcess)
            throw new BillAssessmentValidationException("Final deduction must be between 0.00 and CalculatedExcess.");
        isOverride = finalDeduction != calculatedExcess;
        if (isOverride && string.IsNullOrWhiteSpace(reason))
            throw new BillAssessmentValidationException("A reason is required when the final deduction differs from CalculatedExcess.");
        return finalDeduction;
    }

    private static MonthlyBillAssessmentDto Map(MonthlyBill bill, decimal availableEntitlement) => new(bill.Id, bill.Responsibility, bill.ActualBill, availableEntitlement, bill.Variance, bill.CalculatedExcess, bill.FinalDeduction, bill.AssessedAt, bill.AssessedBy, bill.DeductionOverrideAmount, bill.DeductionOverrideReason, bill.DeductionOverrideBy, bill.DeductionOverrideAt);
}
