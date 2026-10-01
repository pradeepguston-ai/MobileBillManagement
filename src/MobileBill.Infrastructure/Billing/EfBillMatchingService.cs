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

            var allocations = await db.MobileAccounts.Include(x => x.Employee).ThenInclude(x => x.Category)
                .Include(x => x.Employee).ThenInclude(x => x.Designation)
                .Include(x => x.Employee).ThenInclude(x => x.Factory)
                .Include(x => x.Employee).ThenInclude(x => x.Department)
                .Where(x => x.MobileNumber == line.MobileNumber && x.IsActive)
                .ToListAsync(token);
            if (allocations.Count == 0)
            {
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
            if (!allocation.Employee.IsActive)
            {
                exceptions.Add(Exception(batch.Id, line.Id, BillExceptionType.EMPLOYEE_NOT_ACTIVE, BillExceptionSeverity.Blocking,
                    $"The employee allocated to {line.MobileNumber} is inactive."));
                continue;
            }
            monthlyBills.Add(CreateMonthlyBill(line, allocation, AllocationMatchMethod.Automatic, clock.UtcNow, currentUser.UserId));
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
        return new MonthlyBill
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
        MobileNumberSnapshot = line.MobileNumber, 
         AllocationMatchMethod = allocationMethod,
        EntitlementMatchMethod = EntitlementMatchMethod.Automatic, CreatedAtUtc = now, CreatedBy = userId
        };
    }

    internal static BillException Exception(Guid batchId, Guid lineId, BillExceptionType type, BillExceptionSeverity severity, string description) => new()
    { BillBatchId = batchId, BillLineId = lineId, ExceptionType = type, Severity = severity, Description = description };
}
