using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MobileBill.Application.Billing;

namespace MobileBill.Infrastructure.Billing;
public sealed class FileSystemBillFileStorage : IBillFileStorage
{
    private readonly string _root; private readonly long _maximumFileSize;
    public FileSystemBillFileStorage(IOptions<BillStorageOptions> options)
    { _root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.Value.RootPath)); _maximumFileSize = options.Value.MaximumFileSizeBytes; }
    public async Task<StagedBillFile> StageAsync(Stream content, string originalFileName, string? contentType, long length, CancellationToken token)
    {
        if (content is null || length <= 0) throw new BillBatchValidationException("A non-empty PDF file is required.");
        if (length > _maximumFileSize) throw new BillBatchValidationException("The uploaded PDF exceeds the configured maximum file size.");
        if (!string.Equals(Path.GetExtension(originalFileName), ".pdf", StringComparison.OrdinalIgnoreCase) || !string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)) throw new BillBatchValidationException("Only application/pdf files with a .pdf extension are accepted.");
        Directory.CreateDirectory(Path.Combine(_root, "staging")); var path = Path.Combine(_root, "staging", $"{Guid.NewGuid():N}.tmp");
        try { var header = new byte[5]; var read = await content.ReadAsync(header, token); if (read != 5 || !header.SequenceEqual("%PDF-"u8.ToArray())) throw new BillBatchValidationException("The file does not have a valid PDF signature.");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); hash.AppendData(header); await using var target = File.Create(path); await target.WriteAsync(header, token); var buffer = new byte[81920]; int count; long written = header.Length; while ((count = await content.ReadAsync(buffer, token)) > 0) { written += count; if (written > _maximumFileSize) throw new BillBatchValidationException("The uploaded PDF exceeds the configured maximum file size."); hash.AppendData(buffer.AsSpan(0,count)); await target.WriteAsync(buffer.AsMemory(0,count), token); } return new(path, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), length); }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    public Task<string> FinalizeAsync(StagedBillFile staged, Guid batchId, CancellationToken token) { var directory = Path.Combine(_root, batchId.ToString("N")); Directory.CreateDirectory(directory); var final = Path.Combine(directory, $"{staged.FileHash}.pdf"); File.Move(staged.TemporaryPath, final, false); return Task.FromResult(final); }
    public Task<Stream> OpenReadAsync(string path, CancellationToken token) => Task.FromResult<Stream>(File.OpenRead(EnsureInsideRoot(path)));
    public Task DeleteAsync(string path, CancellationToken token) { var safe = EnsureInsideRoot(path); if (File.Exists(safe)) File.Delete(safe); return Task.CompletedTask; }
    private string EnsureInsideRoot(string path) { var full = Path.GetFullPath(path); if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid bill storage path."); return full; }
}
