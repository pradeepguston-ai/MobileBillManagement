using System.Text.Json.Serialization;

namespace MobileBill.Application.Billing;

// Whose history the trend follows: this employee on this number, all of this employee's numbers, or this number
// with every person who held it.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillTrendScope { ThisNumber, Employee, AllHolders }

public sealed record BillTrendRequest(BillTrendScope Scope = BillTrendScope.ThisNumber, int? Months = null);

// One billing month. Amounts are totals when the scope covers several numbers in the month. Responsibility is
// ByUser, ByCompany, Mixed, or null when unassessed.
public sealed record BillTrendPointDto(
    int BillingYear, int BillingMonth, decimal ActualBill, decimal Entitlement, decimal CalculatedExcess, decimal FinalDeduction, decimal Vas,
    string? Responsibility, bool IsOverLimit, bool IsPreliminary, bool IsCurrent, int Numbers, string HolderEpf, string HolderName, bool IsOtherHolder);

public sealed record BillTrendDto(
    Guid MonthlyBillId, string MobileNumber, string EmployeeEpf, string EmployeeName, BillTrendScope Scope, int Months,
    IReadOnlyList<BillTrendPointDto> Points, decimal CurrentActualBill, decimal? Average, decimal? ChangePercent, bool IsAboveUsual,
    int MonthsOverLimit, int? HighestYear, int? HighestMonth, decimal? HighestActualBill, decimal AboveUsualThresholdPercent);

public interface IBillTrendService
{
    Task<BillTrendDto> GetAsync(Guid batchId, Guid monthlyBillId, BillTrendRequest request, CancellationToken cancellationToken);
}
