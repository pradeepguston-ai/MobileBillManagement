namespace MobileBill.Application.MasterData;

public sealed record MobileAllocationReportFile(string FileName, string ContentType, byte[] Content);

// One Excel workbook with a sheet each for assigned numbers, the SIM Pool and disconnected numbers.
public interface IMobileAllocationReportService
{
    Task<MobileAllocationReportFile> ExportAsync(CancellationToken cancellationToken);
}
