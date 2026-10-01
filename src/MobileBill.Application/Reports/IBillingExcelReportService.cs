namespace MobileBill.Application.Reports;

public interface IBillingExcelReportService
{
    Task<BillingExcelReportResult> ExportAsync(
        BillingExcelReportRequest request,
        CancellationToken cancellationToken);
}

// FactoryCodes and CategoryCodes narrow the report (either or both, several of each allowed); leaving both empty produces the full batch report.
public sealed record BillingExcelReportRequest(Guid BatchId, IReadOnlyList<string>? FactoryCodes = null, IReadOnlyList<string>? CategoryCodes = null);

public sealed record BillingExcelReportResult(
    Guid BatchId,
    int BillingYear,
    int BillingMonth,
    string FileName,
    string ContentType,
    byte[] Content,
    decimal BatchCalculatedGrandTotal,
    decimal ExportedActualBillTotal,
    decimal ExcludedActualBillTotal,
    decimal Difference);

public sealed class BillingReportNotFoundException(Guid batchId)
    : Exception($"Bill batch with id '{batchId}' was not found.");

public sealed class BillingReportConflictException(string message) : Exception(message);

// PDF twin of the Excel export: same batch rules and the same figures, returned in the same result shape.
public interface IBillingPdfReportService
{
    Task<BillingExcelReportResult> ExportAsync(
        BillingExcelReportRequest request,
        CancellationToken cancellationToken);
}

public sealed class BillingReportFilterNotFoundException(string kind, string code)
    : Exception($"{kind} '{code}' was not found.");
