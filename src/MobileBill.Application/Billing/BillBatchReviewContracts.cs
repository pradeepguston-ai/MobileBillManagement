using System.Text.Json.Serialization;
using MobileBill.Application.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Billing;

public enum ReviewResponsibilityFilter { Unassessed, ByUser, ByCompany }
public enum ReviewExceptionFilter { All, HasException, NoException }

public sealed record ApprovalHistoryItemDto(Guid Id, [property: JsonConverter(typeof(JsonStringEnumConverter))] ApprovalStage WorkflowStage, [property: JsonConverter(typeof(JsonStringEnumConverter))] ApprovalAction Action, [property: JsonConverter(typeof(JsonStringEnumConverter))] WorkflowRole WorkflowRole, string UserId, string DisplayName, DateTimeOffset Timestamp, string? Comment, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus PreviousStatus, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus NewStatus);
public sealed record BillBatchReviewSummaryDto(Guid BatchId, int BillingYear, int BillingMonth, string Provider, string CorporateCode, [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus BatchStatus, [property: JsonConverter(typeof(JsonStringEnumConverter))] ValidationLevel ValidationLevel, int TotalAccounts, decimal TotalActualBill, decimal TotalCalculatedExcess, decimal TotalFinalDeduction, decimal CompanyResponsibilityAmount, int ExceptionCount, int UnmatchedCount, int AssessedCount, int UnassessedCount)
{
    public IReadOnlyList<ApprovalHistoryItemDto> ApprovalHistory { get; init; } = [];
}
public sealed record BillReviewRowsRequest(int PageNumber = 1, int PageSize = 20, string? SortBy = null, string? SortDirection = null, string? FactoryCode = null, string? DepartmentCode = null, string? CategoryCode = null, ReviewResponsibilityFilter? Responsibility = null, ReviewExceptionFilter Exception = ReviewExceptionFilter.All, MonthlyBillStatus? Status = null, string? Search = null, decimal? CalculatedExcessMin = null, decimal? CalculatedExcessMax = null)
{ public int NormalizedPageNumber => Math.Max(1, PageNumber); public int NormalizedPageSize => Math.Clamp(PageSize, 1, 100); }
public sealed record BillReviewRowDto(Guid Id, string MobileNumber, string EmployeeEpf, string EmployeeName, string? CallingName, string Category, string Designation, string Factory, string Department, decimal CreditLimit, decimal MonthlyRental, decimal AvailableEntitlement, decimal ActualBill, decimal Variance, decimal CalculatedExcess, [property: JsonConverter(typeof(JsonStringEnumConverter))] Responsibility? Responsibility, decimal FinalDeduction, string? Remark, [property: JsonConverter(typeof(JsonStringEnumConverter))] MonthlyBillStatus Status, bool HasException, bool IsAssessed);
public sealed record ChargeBreakdownDto(decimal PreviousDue, decimal Payments, decimal TotalUsage, decimal Idd, decimal Roaming, decimal Vas, decimal Discounts, decimal BillAdjustments, decimal CommitmentCharges, decimal LatePaymentCharges, decimal AddToBill, decimal InstalmentPlans, decimal GovernmentTaxesLevies, decimal Vat, decimal ChargesForBillPeriod, decimal TotalDueAmount);
public sealed record BillReviewDetailDto(BillReviewRowDto Row, ChargeBreakdownDto Charges, IReadOnlyList<string> Exceptions, IReadOnlyList<string> ApprovalHistory, IReadOnlyList<string> AuditHistory, string? AssessedBy = null, DateTimeOffset? AssessedAt = null, string? DeductionOverrideReason = null);
public interface IBillBatchReviewQueryService
{ Task<BillBatchReviewSummaryDto> GetSummaryAsync(Guid batchId, CancellationToken cancellationToken); Task<PagedResult<BillReviewRowDto>> GetRowsAsync(Guid batchId, BillReviewRowsRequest request, CancellationToken cancellationToken); Task<BillReviewDetailDto> GetDetailAsync(Guid batchId, Guid monthlyBillId, CancellationToken cancellationToken); }
