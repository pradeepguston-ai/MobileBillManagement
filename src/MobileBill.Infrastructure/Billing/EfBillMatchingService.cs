using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Domain.Calculations;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Billing;

public sealed class EfBillMatchingService(MobileBillDbContext db, IClock clock, ICurrentUserService currentUser, IBillReviewAuthorizationService authorization) : IBillMatchingService
{
    public async Task<BillMatchingResult> MatchAsync(Guid billBatchId, CancellationToken token)
    {
        if (!await authorization.AuthorizeAsync(BillWorkflowAction.PrepareBatch, token)) throw new BillReviewForbiddenException();
        var batch = await db.BillBatches.SingleOrDefaultAsync(x => x.Id == billBatchId, token)
            ?? throw new BillBatchNotFoundException(billBatchId);
        if (batch.Status != BillBatchStatus.Validated)
            throw new BillReviewConflictException("Only a Validated bill batch can be matched.");
        if (await db.MonthlyBills.AnyAsync(x => x.BillLine.BillBatchId == billBatchId, token)
            || await db.BillExceptions.AnyAsync(x => x.BillBatchId == billBatchId, token))
            throw new BillReviewConflictException("This batch has already been matched. Matching cannot be repeated.");



        var lines = await db.BillLines.Where(x => x.BillBatchId == billBatchId).OrderBy(x => x.PageNumber).ThenBy(x => x.Id).ToListAsync(token);
        var monthlyBills = new List<MonthlyBill>();
        var exceptions = new List<BillException>();

        foreach (var line in lines)
        {
            if (line.ExtractionStatus != BillLineExtractionStatus.Extracted)
            {
                exceptions.Add(Exception(batch.Id, line.Id, BillExceptionType.PARSER_WARNING, BillExceptionSeverity.Warning,
                    $"Parser candidate on page {line.PageNumber} was not successfully extracted: {line.ExtractionError ?? "unknown parsing issue"}."));
                continue;
            }
            if (line.TotalDueAmount == 0m)
                exceptions.Add(Exception(batch.Id, line.Id, BillExceptionType.ZERO_BILL, BillExceptionSeverity.Information,
                    $"Mobile number {line.MobileNumber} has a total due amount of 0.00 and was retained for review."));

            var allocations = await WithEmployee(db.MobileAccounts)
                .Where(x => x.MobileNumber == line.MobileNumber && x.IsActive)
                .ToListAsync(token);
            if (allocations.Count == 0)
            {
                // A disconnected SIM still being billed: the company pays, and the line is flagged for a provider follow-up.
                var disconnected = await WithEmployee(db.MobileAccounts)
                    .Where(x => x.MobileNumber == line.MobileNumber && x.Status == SimStatus.Disconnected)
                    .OrderByDescending(x => x.DisconnectedOn).ThenByDescending(x => x.UpdatedAtUtc)
                    .FirstOrDefaultAsync(token);
                if (disconnected is not null)
                {
                    var companyBill = CreatePooledBill(line, disconnected, AllocationMatchMethod.Automatic, clock.UtcNow, currentUser.UserId,
                        $"Billed after disconnection on {disconnected.DisconnectedOn:dd-MMM-yyyy} · previously EPF {disconnected.Employee.EPF} – {disconnected.Employee.FullName}");
                    monthlyBills.Add(companyBill);
                    db.AuditLogs.Add(AutoAssessmentAudit(companyBill, clock.UtcNow, currentUser.UserId)!);
                    var flag = Exception(batch.Id, line.Id, BillExceptionType.BILLED_AFTER_DISCONNECTION, BillExceptionSeverity.Warning,
                        $"{line.MobileNumber} was disconnected on {disconnected.DisconnectedOn:dd-MMM-yyyy} but is still billed. Check with the provider.");
                    flag.MonthlyBillId = companyBill.Id;
                    exceptions.Add(flag);
                    continue;
                }
                exceptions.Add(Exception(batch.Id, line.Id, BillExceptionType.MOBILE_NOT_FOUND, BillExceptionSeverity.Blocking,
                    $"No active mobile allocation exists for {line.MobileNumber}."));
                continue;
            }
            if (allocations.Count > 1)
            {
                exceptions.Add(Exception(batch.Id, line.Id, BillExceptionType.MULTIPLE_ACTIVE_ALLOCATIONS, BillExceptionSeverity.Blocking,
                    $"Multiple active mobile allocations exist for {line.MobileNumber}; no allocation was selected."));
                continue;
            }

            var allocation = allocations[0];
            if (allocation.Status == SimStatus.Pooled)
            {
                var pooledBill = CreateBillForPooledSim(line, allocation, batch.BillingYear, batch.BillingMonth, AllocationMatchMethod.Automatic, clock.UtcNow, currentUser.UserId);
                monthlyBills.Add(pooledBill);
                db.AuditLogs.Add(AutoAssessmentAudit(pooledBill, clock.UtcNow, currentUser.UserId)!);
                continue;
            }
            if (!allocation.Employee.IsActive)
            {
                exceptions.Add(Exception(batch.Id, line.Id, BillExceptionType.EMPLOYEE_NOT_ACTIVE, BillExceptionSeverity.Blocking,
                    $"The employee allocated to {line.MobileNumber} is inactive."));
                continue;
            }
            var monthlyBill = CreateMonthlyBill(line, allocation, AllocationMatchMethod.Automatic, clock.UtcNow, currentUser.UserId);
            monthlyBills.Add(monthlyBill);
            if (AutoAssessmentAudit(monthlyBill, clock.UtcNow, currentUser.UserId) is { } audit) db.AuditLogs.Add(audit);
        }

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(token) : null;
        db.MonthlyBills.AddRange(monthlyBills);
        db.BillExceptions.AddRange(exceptions);
        await db.SaveChangesAsync(token);
        if (transaction is not null) await transaction.CommitAsync(token);
        return new BillMatchingResult(batch.Id, monthlyBills.Count, exceptions.Count);
    }

    internal static MonthlyBill CreateMonthlyBill(BillLine line, MobileAccount allocation, AllocationMatchMethod allocationMethod, DateTimeOffset now, string userId)
    {
        var calculation = MonthlyBillCalculation.Create(line.TotalDueAmount, allocation.MonthlyCreditLimit, allocation.MonthlyRental);
        var bill = new MonthlyBill
        {
        EmployeeId = allocation.Employee.Id, MobileAccountId = allocation.Id, BillLineId = line.Id,
        CreditLimit = allocation.MonthlyCreditLimit, MonthlyRental = allocation.MonthlyRental, ActualBill = calculation.ActualBill,
        Variance = calculation.Variance, CalculatedExcess = calculation.CalculatedExcess,
        FinalDeduction = 0m, EmployeeEpfSnapshot = allocation.Employee.EPF, EmployeeNameSnapshot = allocation.Employee.FullName,
        CallingNameSnapshot = allocation.Employee.CallingName, CategoryCodeSnapshot = allocation.Employee.CategoryCode,
        CategoryNameSnapshot = allocation.Employee.Category.Name, DesignationCodeSnapshot = allocation.Employee.DesignationCode,
        DesignationNameSnapshot = allocation.Employee.Designation.Name, FactoryCodeSnapshot = allocation.Employee.FactoryCode,
        FactoryNameSnapshot = allocation.Employee.Factory.Name, DepartmentCodeSnapshot = allocation.Employee.DepartmentCode,
        DepartmentNameSnapshot = allocation.Employee.Department.Name,
        SectionCodeSnapshot = allocation.Employee.SectionCode, SectionNameSnapshot = allocation.Employee.Section?.Name,
        SubSectionCodeSnapshot = allocation.Employee.SubSectionCode, SubSectionNameSnapshot = allocation.Employee.SubSection?.Name,
        MobileNumberSnapshot = line.MobileNumber,
         AllocationMatchMethod = allocationMethod,
        EntitlementMatchMethod = EntitlementMatchMethod.Automatic, CreatedAtUtc = now, CreatedBy = userId
        };
        if (allocation.Employee.DefaultResponsibility is { } remembered)
        {
            bill.Responsibility = remembered;
            bill.FinalDeduction = remembered == Responsibility.ByCompany ? 0m : calculation.CalculatedExcess;
            bill.AssessedAt = now;
            bill.AssessedBy = userId;
        }
        return bill;
    }

    internal const string SimPoolEmployeeEpf = "POOL";
    internal const string SimPoolEmployeeName = "SIM Pool (Unassigned)";

    internal static IQueryable<MobileAccount> WithEmployee(IQueryable<MobileAccount> query) => query
        .Include(x => x.Employee).ThenInclude(x => x.Category)
        .Include(x => x.Employee).ThenInclude(x => x.Designation)
        .Include(x => x.Employee).ThenInclude(x => x.Factory)
        .Include(x => x.Employee).ThenInclude(x => x.Department)
        .Include(x => x.Employee).ThenInclude(x => x.Section)
        .Include(x => x.Employee).ThenInclude(x => x.SubSection);

    // A pooled SIM's bill: the company pays if its last holder resigned before the 10th of the billing month;
    // otherwise it is that employee's last month and is charged to them as usual.
    internal static MonthlyBill CreateBillForPooledSim(BillLine line, MobileAccount allocation, int billingYear, int billingMonth, AllocationMatchMethod allocationMethod, DateTimeOffset now, string userId)
    {
        var resignedOn = allocation.PooledOn ?? DateOnly.FromDateTime(now.UtcDateTime);
        var previous = $"EPF {allocation.Employee.EPF} – {allocation.Employee.FullName}";
        if (SimPoolRules.PayerFor(resignedOn, billingYear, billingMonth) == PooledBillPayer.Company)
            return CreatePooledBill(line, allocation, allocationMethod, now, userId, $"SIM Pool since {resignedOn:dd-MMM-yyyy} · previously {previous}");

        var bill = CreateMonthlyBill(line, allocation, allocationMethod, now, userId);
        bill.Responsibility = Responsibility.ByUser;
        bill.FinalDeduction = bill.CalculatedExcess;
        bill.Remark = $"Resigned on {resignedOn:dd-MMM-yyyy} (on or after the {SimPoolRules.CompanyPaysIfResignedBeforeDay}th): final month charged to employee";
        bill.AssessedAt = now;
        bill.AssessedBy = userId;
        return bill;
    }

    // Company-paid bill with no current holder; the organisation columns stay with the last holder so factory and department totals still include it.
    internal static MonthlyBill CreatePooledBill(BillLine line, MobileAccount allocation, AllocationMatchMethod allocationMethod, DateTimeOffset now, string userId, string remark)
    {
        var bill = CreateMonthlyBill(line, allocation, allocationMethod, now, userId);
        bill.IsPooled = true;
        bill.EmployeeEpfSnapshot = SimPoolEmployeeEpf;
        bill.EmployeeNameSnapshot = SimPoolEmployeeName;
        bill.CallingNameSnapshot = null;
        bill.Responsibility = Responsibility.ByCompany;
        bill.FinalDeduction = 0m;
        bill.Remark = remark;
        bill.AssessedAt = now;
        bill.AssessedBy = userId;
        return bill;
    }

    internal static AuditLog? AutoAssessmentAudit(MonthlyBill bill, DateTimeOffset now, string userId) => bill.Responsibility is null ? null : new AuditLog
    {
        EntityName = nameof(MonthlyBill), EntityId = bill.Id,
        Action = bill.IsPooled ? "AutoAssessedSimPool" : bill.Remark?.StartsWith("Resigned on ", StringComparison.Ordinal) == true ? "AutoAssessedResignedHolder" : "AutoAssessedFromPreviousBatch",
        BeforeDataJson = "{}",
        AfterDataJson = System.Text.Json.JsonSerializer.Serialize(new { Responsibility = bill.Responsibility.ToString(), bill.FinalDeduction, bill.Remark }),
        PerformedBy = userId, PerformedAt = now, CreatedAtUtc = now, CreatedBy = userId
    };

    internal static BillException Exception(Guid batchId, Guid lineId, BillExceptionType type, BillExceptionSeverity severity, string description) => new()
    { BillBatchId = batchId, BillLineId = lineId, ExceptionType = type, Severity = severity, Description = description };
}
