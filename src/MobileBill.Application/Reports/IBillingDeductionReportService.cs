using MobileBill.Application.Billing;

namespace MobileBill.Application.Reports;

// The payroll deduction sheet opened from Monthly Bill Report > Open Report: one row per employee with the total
// By User deduction of all their numbers. Filters are the Monthly Bill Review filters, so it matches the screen.
// Only Completed or Locked batches, as the deductions are final only after Finance approval.
public interface IBillingDeductionReportService
{
    Task<DeductionReportFile> ExportAsync(Guid batchId, BillReviewRowsRequest filters, CancellationToken cancellationToken);

    // The sheet attached to a Report Distribution email: the rule's factories and categories (empty means all).
    Task<DeductionReportFile> ExportForRuleAsync(Guid batchId, IReadOnlyCollection<string>? factoryCodes, IReadOnlyCollection<string>? categoryCodes, CancellationToken cancellationToken);
}

public sealed record DeductionReportFile(string FileName, string ContentType, byte[] Content, int EmployeeCount, decimal TotalDeduction);
