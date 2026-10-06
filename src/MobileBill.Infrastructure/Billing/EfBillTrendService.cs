using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Billing;
using MobileBill.Domain.Calculations;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Billing;

// Month-by-month bill history behind one reviewed bill, from the matched batches up to and including its own batch.
// Read-only: nothing is saved.
public sealed class EfBillTrendService(MobileBillDbContext db) : IBillTrendService
{
    public async Task<BillTrendDto> GetAsync(Guid batchId, Guid monthlyBillId, BillTrendRequest request, CancellationToken cancellationToken)
    {
        var current = await db.MonthlyBills.AsNoTracking()
            .Where(bill => bill.Id == monthlyBillId && bill.BillLine.BillBatchId == batchId)
            .Select(bill => new { bill.EmployeeId, bill.MobileNumberSnapshot, bill.EmployeeEpfSnapshot, bill.EmployeeNameSnapshot, bill.BillLine.BillBatch.BillingYear, bill.BillLine.BillBatch.BillingMonth })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new BillReviewRowNotFoundException(monthlyBillId);

        var months = BillTrendRules.NormalizeMonths(request.Months);
        var currentPeriod = current.BillingYear * 12 + current.BillingMonth;
        var firstPeriod = currentPeriod - months + 1;
        var bills = db.MonthlyBills.AsNoTracking()
            .Where(bill => bill.BillLine.BillBatch.BillingYear * 12 + bill.BillLine.BillBatch.BillingMonth >= firstPeriod
                && bill.BillLine.BillBatch.BillingYear * 12 + bill.BillLine.BillBatch.BillingMonth <= currentPeriod);
        bills = request.Scope switch
        {
            BillTrendScope.Employee => bills.Where(bill => bill.EmployeeId == current.EmployeeId),
            BillTrendScope.AllHolders => bills.Where(bill => bill.MobileNumberSnapshot == current.MobileNumberSnapshot),
            _ => bills.Where(bill => bill.EmployeeId == current.EmployeeId && bill.MobileNumberSnapshot == current.MobileNumberSnapshot),
        };
        var rows = await bills.Select(bill => new
        {
            bill.BillLine.BillBatch.BillingYear, bill.BillLine.BillBatch.BillingMonth, BatchStatus = bill.BillLine.BillBatch.Status,
            bill.EmployeeId, bill.EmployeeEpfSnapshot, bill.EmployeeNameSnapshot, bill.MobileNumberSnapshot,
            bill.ActualBill, bill.CreditLimit, bill.MonthlyRental, bill.CalculatedExcess, bill.FinalDeduction, bill.Responsibility,
            Vas = bill.BillLine.ValueAddedServices
        }).ToListAsync(cancellationToken);

        var points = rows
            .GroupBy(row => (row.BillingYear, row.BillingMonth))
            .OrderBy(group => group.Key.BillingYear).ThenBy(group => group.Key.BillingMonth)
            .Select(group =>
            {
                var holders = group.Select(row => (row.EmployeeId, row.EmployeeEpfSnapshot, row.EmployeeNameSnapshot)).Distinct().OrderBy(holder => holder.EmployeeEpfSnapshot, StringComparer.Ordinal).ToList();
                var responsibilities = group.Select(row => row.Responsibility).Distinct().ToList();
                return new BillTrendPointDto(
                    group.Key.BillingYear, group.Key.BillingMonth,
                    group.Sum(row => row.ActualBill), group.Sum(row => row.CreditLimit + row.MonthlyRental),
                    group.Sum(row => row.CalculatedExcess), group.Sum(row => row.FinalDeduction), group.Sum(row => row.Vas),
                    responsibilities.Count == 1 ? responsibilities[0]?.ToString() : responsibilities.Contains(null) ? null : "Mixed",
                    group.Any(row => BillTrendRules.IsOverLimit(row.ActualBill, row.CreditLimit, row.MonthlyRental)),
                    group.Any(row => row.BatchStatus is not (BillBatchStatus.Completed or BillBatchStatus.Locked)),
                    group.Key.BillingYear * 12 + group.Key.BillingMonth == currentPeriod,
                    group.Select(row => row.MobileNumberSnapshot).Distinct().Count(),
                    string.Join(", ", holders.Select(holder => holder.EmployeeEpfSnapshot)),
                    string.Join(", ", holders.Select(holder => holder.EmployeeNameSnapshot)),
                    holders.Any(holder => holder.EmployeeId != current.EmployeeId));
            })
            .ToList();

        var currentPoint = points.SingleOrDefault(point => point.IsCurrent);
        var currentActual = currentPoint?.ActualBill ?? 0m;
        var average = BillTrendRules.Average(points.Where(point => !point.IsCurrent).Select(point => point.ActualBill));
        var highest = points.OrderByDescending(point => point.ActualBill).ThenByDescending(point => point.BillingYear * 12 + point.BillingMonth).FirstOrDefault();

        return new BillTrendDto(monthlyBillId, current.MobileNumberSnapshot, current.EmployeeEpfSnapshot, current.EmployeeNameSnapshot, request.Scope, months,
            points, currentActual, average, BillTrendRules.ChangePercent(currentActual, average), BillTrendRules.IsAboveUsual(currentActual, average),
            points.Count(point => point.IsOverLimit), highest?.BillingYear, highest?.BillingMonth, highest?.ActualBill, BillTrendRules.AboveUsualThresholdPercent);
    }
}
