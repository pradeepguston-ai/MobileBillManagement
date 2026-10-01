namespace MobileBill.Infrastructure.Billing;
public sealed record StagedBillFile(string TemporaryPath, string FileHash, long Length);
public interface IBillFileStorage
{
    Task<StagedBillFile> StageAsync(Stream content, string originalFileName, string? contentType, long length, CancellationToken cancellationToken);
    Task<string> FinalizeAsync(StagedBillFile stagedFile, Guid batchId, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storedFilePath, CancellationToken cancellationToken);
    Task DeleteAsync(string filePath, CancellationToken cancellationToken);
}
