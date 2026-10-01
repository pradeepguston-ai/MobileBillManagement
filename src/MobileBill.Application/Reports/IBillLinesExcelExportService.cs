namespace MobileBill.Application.Reports;

// Every extracted line of a batch (the Extracted Bill Lines screen) as an Excel file. Search narrows it
// to matching mobile numbers, the same as the screen's search box.
public interface IBillLinesExcelExportService
{
    Task<BillLinesExcelExportResult> ExportAsync(Guid batchId, string? search, CancellationToken cancellationToken);
}

public sealed record BillLinesExcelExportResult(string FileName, string ContentType, byte[] Content, int LineCount);
