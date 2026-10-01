namespace MobileBill.Application.Reports;

public interface IBillingExcelReportService
{
    Task<BillingExcelReportResult> ExportAsync(
        BillingExcelReportRequest request,
        CancellationToken cancellationToken);
}

public sealed record BillingExcelReportRequest(Guid BatchId);

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
