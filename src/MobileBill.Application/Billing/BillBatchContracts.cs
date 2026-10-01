using MobileBill.Application.Common;
using MobileBill.Domain.Enums;
using System.Text.Json.Serialization;

namespace MobileBill.Application.Billing;

public sealed record CreateBillBatchRequest(Guid ProviderId, string CorporateCode, int BillingYear, int BillingMonth);
public sealed record BillBatchListRequest(int Page = 1, int PageSize = 20, string? SortBy = null, string? SortDirection = null, BillBatchStatus? Status = null, int? BillingYear = null, int? BillingMonth = null, Guid? ProviderId = null, string? Search = null)
{
    public int NormalizedPage => Math.Max(1, Page);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 100);
}
public sealed record BillBatchListItemDto(Guid Id, Guid ProviderId, string Provider, string CorporateCode, int BillingYear, int BillingMonth, decimal? CalculatedGrandTotal, [property: JsonConverter(typeof(JsonStringEnumConverter))] ValidationLevel ValidationLevel, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus Status, string? OriginalFileName, string? UploadedBy, DateTimeOffset? UploadedAt);
public sealed record BillBatchDto(Guid Id, Guid ProviderId, string CorporateCode, int BillingYear, int BillingMonth, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus Status, string? OriginalFileName, string? FileHash, string? UploadedBy, DateTimeOffset? UploadedAt, decimal? StatedGrandTotal, decimal? CalculatedGrandTotal, decimal? Difference, [property: JsonConverter(typeof(JsonStringEnumConverter))] GrandTotalSource GrandTotalSource, [property: JsonConverter(typeof(JsonStringEnumConverter))] ValidationLevel ValidationLevel, string? ValidationWarning, string? ProviderName = null, int TotalCandidates = 0, int SuccessfulCount = 0, int FailedCount = 0, IReadOnlyList<string>? Warnings = null);
public sealed record BillLineDto(Guid Id, string MobileNumber, int PageNumber, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillLineExtractionStatus ExtractionStatus, string? ExtractionError, decimal PreviousDueAmount, decimal Payments, decimal TotalUsageCharges, decimal Idd, decimal Roaming, decimal ValueAddedServices, decimal Discounts, decimal BillAdjustmentsBalanceTransfers, decimal CommitmentCharges, decimal LatePaymentCharges, decimal AddToBill, decimal InstalmentPlans, decimal GovernmentTaxesAndLevies, decimal Vat, decimal ChargesForBillPeriod, decimal TotalDueAmount, string RawText);
