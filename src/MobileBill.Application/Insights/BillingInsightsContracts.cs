using System.Text.Json.Serialization;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Insights;

// Figures for the Dashboard's Insights tab. Everything is calculated on the server; the screen only draws it.
public interface IBillingInsightsService
{
    // batchId null means the latest batch that has monthly bills. months is how many approved batches the trend covers.
    Task<BillingInsightsDto> GetAsync(Guid? batchId, int months, CancellationToken cancellationToken);
}

public sealed record BillingInsightsDto(
    IReadOnlyList<InsightsBatchOption> Batches,
    InsightsBatchOption? Selected,
    InsightsKpis? Current,
    InsightsKpis? Previous,
    InsightsBatchOption? PreviousBatch,
    IReadOnlyList<InsightsAmount> ChargeMix,
    IReadOnlyList<InsightsAmount> ExcessSplit,
    InsightsGroups Groups,
    IReadOnlyList<InsightsTopAccount> TopOverLimit,
    IReadOnlyList<InsightsTrendPoint> Trend);

// IsApproved is false for a batch still in review; its figures are provisional.
public sealed record InsightsBatchOption(Guid Id, int BillingYear, int BillingMonth, string Provider, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus Status, bool IsApproved);

public sealed record InsightsKpis(
    decimal TotalActualBill,
    decimal TotalEntitlement,
    decimal TotalCalculatedExcess,
    decimal DeductedFromEmployees,
    decimal BorneByCompany,
    decimal UnassessedExcess,
    decimal TotalRoaming,
    int Accounts,
    int OverLimitAccounts);

public sealed record InsightsAmount(string Key, string Label, decimal Amount);

public sealed record InsightsGroupRow(string Code, string Name, int Accounts, decimal ActualBill, decimal Entitlement, decimal CalculatedExcess);

public sealed record InsightsGroups(IReadOnlyList<InsightsGroupRow> Factory, IReadOnlyList<InsightsGroupRow> Department, IReadOnlyList<InsightsGroupRow> Category);

public sealed record InsightsTopAccount(
    string MobileNumber,
    string Epf,
    string EmployeeName,
    string? CallingName,
    string Factory,
    string Department,
    decimal ActualBill,
    decimal Entitlement,
    decimal CalculatedExcess,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] Responsibility? Responsibility,
    string? Remark);

public sealed record InsightsTrendPoint(
    Guid BatchId,
    int BillingYear,
    int BillingMonth,
    bool IsApproved,
    decimal TotalActualBill,
    decimal TotalCalculatedExcess,
    decimal DeductedFromEmployees,
    decimal BorneByCompany,
    int Accounts);
