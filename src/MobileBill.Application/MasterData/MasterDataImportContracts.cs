namespace MobileBill.Application.MasterData;

// Row numbers are the Excel row numbers, so users can find the line in their file.
public sealed record ImportRowError(int Row, string Message);

// Committed is false when the file was only checked, or when any row has an error (nothing is saved then).
public sealed record MasterDataImportResult(int TotalRows, int NewCount, int UpdatedCount, int UnchangedCount, IReadOnlyList<ImportRowError> Errors, bool Committed);

public sealed record ImportTemplate(string FileName, byte[] Content);

// Bulk upload from Excel. Employees are identified by Factory Code + EPF; mobile allocations name their holder the same
// way and the system resolves the employee ID. Every row is validated first, and the file is saved all-or-nothing.
public interface IMasterDataImportService
{
    ImportTemplate EmployeeTemplate();
    ImportTemplate MobileAccountTemplate();
    Task<MasterDataImportResult> ImportEmployeesAsync(Stream workbook, bool commit, CancellationToken cancellationToken);
    Task<MasterDataImportResult> ImportMobileAccountsAsync(Stream workbook, bool commit, CancellationToken cancellationToken);
}
