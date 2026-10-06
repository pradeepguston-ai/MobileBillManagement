using System.Text.Json.Serialization;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Reports;

// Value Added Services (VAS) report: every number with a VAS charge in one matched batch. Factory, category and
// section filters work like the Monthly Bill Report; MinimumVas and RepeatOnly narrow the list further.
public sealed record VasReportRequest(
    Guid BatchId,
    IReadOnlyList<string>? FactoryCodes = null,
    IReadOnlyList<string>? CategoryCodes = null,
    IReadOnlyList<string>? SectionCodes = null,
    decimal? MinimumVas = null,
    bool RepeatOnly = false);

// Employee columns are empty for a line that was not matched to an allocation. MonthsWithVas counts the months,
// out of MonthsConsidered (up to 3, this month included), in which the number had VAS.
public sealed record VasReportRowDto(
    string MobileNumber, string? Epf, string? EmployeeName, string? CallingName,
    string? Factory, string? Department, string? Section, string? Category,
    decimal Vas, decimal ActualBill, decimal VasShareOfBill,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] Responsibility? Responsibility, decimal? FinalDeduction,
    int MonthsWithVas, int MonthsConsidered, bool IsRepeat, bool IsPooled, bool IsMatched);

public sealed record VasGroupDto(string Name, int Users, decimal TotalVas);

// IsPreliminary: the batch is matched but not yet Completed or Locked, so assessments can still change.
// Previous*: the matched batch before this one, for the month-on-month change (null when there is none).
public sealed record VasReportDto(
    Guid BatchId, int BillingYear, int BillingMonth, string Provider,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus BatchStatus, bool IsPreliminary,
    int Users, decimal TotalVas, int RepeatUsers, int? PreviousUsers, decimal? PreviousTotalVas,
    IReadOnlyList<VasGroupDto> ByFactory, IReadOnlyList<VasGroupDto> ByDepartment, IReadOnlyList<VasReportRowDto> Rows);

public sealed record VasBatchDto(Guid BatchId, int BillingYear, int BillingMonth, string Provider,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] BillBatchStatus BatchStatus, bool IsPreliminary);

public sealed record ReportFile(string FileName, string ContentType, byte[] Content);

public interface IVasReportService
{
    // Matched batches, newest first: the months the VAS report can be produced for.
    Task<IReadOnlyList<VasBatchDto>> GetBatchesAsync(CancellationToken cancellationToken);
    Task<VasReportDto> GetAsync(VasReportRequest request, CancellationToken cancellationToken);
    Task<ReportFile> ExportExcelAsync(VasReportRequest request, CancellationToken cancellationToken);
    Task<ReportFile> ExportPdfAsync(VasReportRequest request, CancellationToken cancellationToken);
}
