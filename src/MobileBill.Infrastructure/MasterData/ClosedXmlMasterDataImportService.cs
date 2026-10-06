using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.MasterData;

// Excel bulk upload for employees and mobile allocations. The first worksheet is read; row 1 holds the column
// headings (matched by name, in any order). Every row is validated against the database and the rest of the file
// before anything is saved, and the whole file is saved in one transaction or not at all.
public sealed class ClosedXmlMasterDataImportService(MobileBillDbContext db, ICurrentUserService currentUser, IClock clock) : IMasterDataImportService
{
    public const int MaxRows = 5000;

    private static readonly string[] EmployeeColumns = ["Factory Code", "EPF", "Full Name", "Calling Name", "Category Code", "Designation Code", "Department Code", "Section Code", "Sub Section Code"];
    private static readonly string[] EmployeeRequired = ["Factory Code", "EPF", "Full Name", "Category Code", "Designation Code", "Department Code"];
    private static readonly string[] AllocationColumns = ["Mobile Number", "Factory Code", "EPF", "Monthly Credit Limit", "Monthly Rental"];

    public ImportTemplate EmployeeTemplate() => new("Employees_Import_Template.xlsx", Template("Employees", EmployeeColumns, EmployeeRequired, ["Factory Code", "EPF"],
    [
        "One row per employee. An employee is identified by Factory Code + EPF: the same EPF may exist in different factories.",
        "If Factory Code + EPF already exists, that employee is updated; otherwise a new employee is created.",
        "Codes must match active records in Factories, Categories, Designations and Departments.",
        "Section Code is optional and must belong to the Department. Sub Section Code is optional and must belong to the Section.",
        "Columns marked * are required. Keep the heading row as it is.",
    ]));

    public ImportTemplate MobileAccountTemplate() => new("Mobile_Allocations_Import_Template.xlsx", Template("Mobile Allocations", AllocationColumns, AllocationColumns, ["Mobile Number", "Factory Code", "EPF"],
    [
        "One row per mobile number. The holder is identified by Factory Code + EPF and must be an active employee.",
        "A number with no active allocation is allocated to the holder.",
        "A number already allocated to the same holder has its credit limit and rental updated.",
        "A number allocated to someone else, or in the SIM Pool, is rejected: use Reassign or Assign from Pool on the screen.",
        "Amounts must be 0 or more, with at most two decimal places. All columns are required.",
    ]));

    public async Task<MasterDataImportResult> ImportEmployeesAsync(Stream workbook, bool commit, CancellationToken cancellationToken)
    {
        var sheet = await ReadSheetAsync(workbook, EmployeeRequired, cancellationToken);
        var factories = await CodesAsync(db.Factories.Where(x => x.IsActive).Select(x => x.Code), cancellationToken);
        var categories = await CodesAsync(db.EmployeeCategories.Where(x => x.IsActive).Select(x => x.Code), cancellationToken);
        var designations = await CodesAsync(db.Designations.Where(x => x.IsActive).Select(x => x.Code), cancellationToken);
        var departments = await CodesAsync(db.Departments.Where(x => x.IsActive).Select(x => x.Code), cancellationToken);
        var sections = (await db.Sections.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Code, x.DepartmentCode }).ToListAsync(cancellationToken))
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var subSections = (await db.SubSections.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Code, x.SectionCode }).ToListAsync(cancellationToken))
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var existing = (await db.Employees.ToListAsync(cancellationToken)).ToDictionary(x => Key(x.FactoryCode, x.EPF), StringComparer.OrdinalIgnoreCase);

        var errors = new List<ImportRowError>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int created = 0, updated = 0, unchanged = 0;
        foreach (var row in sheet.Rows)
        {
            var rowErrors = new List<string>();
            var factory = Code(row.Text("Factory Code"), factories, "Factory Code", rowErrors);
            var epf = Required(row.Text("EPF"), "EPF", 50, rowErrors);
            var fullName = Required(row.Text("Full Name"), "Full Name", 200, rowErrors);
            var callingName = Optional(row.Text("Calling Name"), "Calling Name", 100, rowErrors);
            var category = Code(row.Text("Category Code"), categories, "Category Code", rowErrors);
            var designation = Code(row.Text("Designation Code"), designations, "Designation Code", rowErrors);
            var department = Code(row.Text("Department Code"), departments, "Department Code", rowErrors);
            string? section = null, subSection = null;
            var sectionText = row.Text("Section Code");
            var subSectionText = row.Text("Sub Section Code");
            if (sectionText.Length > 0)
            {
                if (!sections.TryGetValue(sectionText, out var found)) rowErrors.Add($"Section Code '{sectionText}' is not an active section.");
                else if (department is not null && !string.Equals(found.DepartmentCode, department, StringComparison.OrdinalIgnoreCase)) rowErrors.Add($"Section '{found.Code}' does not belong to department '{department}'.");
                else section = found.Code;
            }
            if (subSectionText.Length > 0)
            {
                if (sectionText.Length == 0) rowErrors.Add("Sub Section Code needs a Section Code.");
                else if (!subSections.TryGetValue(subSectionText, out var found)) rowErrors.Add($"Sub Section Code '{subSectionText}' is not an active sub section.");
                else if (section is not null && !string.Equals(found.SectionCode, section, StringComparison.OrdinalIgnoreCase)) rowErrors.Add($"Sub section '{found.Code}' does not belong to section '{section}'.");
                else subSection = found.Code;
            }
            if (factory is not null && epf is not null)
            {
                if (seen.TryGetValue(Key(factory, epf), out var firstRow)) rowErrors.Add($"Factory {factory} + EPF {epf} also appears on row {firstRow}.");
                else seen[Key(factory, epf)] = row.Number;
            }
            if (rowErrors.Count > 0) { errors.AddRange(rowErrors.Select(message => new ImportRowError(row.Number, message))); continue; }

            if (existing.TryGetValue(Key(factory!, epf!), out var employee))
            {
                var changed = employee.FullName != fullName || employee.CallingName != callingName || employee.CategoryCode != category || employee.DesignationCode != designation
                    || employee.DepartmentCode != department || employee.SectionCode != section || employee.SubSectionCode != subSection;
                if (!changed) { unchanged++; continue; }
                employee.FullName = fullName!; employee.CallingName = callingName; employee.CategoryCode = category!; employee.DesignationCode = designation!;
                employee.DepartmentCode = department!; employee.SectionCode = section; employee.SubSectionCode = subSection;
                employee.UpdatedAtUtc = clock.UtcNow; employee.UpdatedBy = currentUser.UserId;
                updated++;
            }
            else
            {
                db.Employees.Add(new Employee { EPF = epf!, FactoryCode = factory!, FullName = fullName!, CallingName = callingName, CategoryCode = category!, DesignationCode = designation!, DepartmentCode = department!, SectionCode = section, SubSectionCode = subSection, CreatedAtUtc = clock.UtcNow, CreatedBy = currentUser.UserId });
                created++;
            }
        }
        return await FinishAsync(nameof(Employee), "EmployeesImported", sheet.Rows.Count, created, updated, unchanged, errors, commit, cancellationToken);
    }

    public async Task<MasterDataImportResult> ImportMobileAccountsAsync(Stream workbook, bool commit, CancellationToken cancellationToken)
    {
        var sheet = await ReadSheetAsync(workbook, AllocationColumns, cancellationToken);
        var employees = (await db.Employees.AsNoTracking().Select(x => new { x.Id, x.FactoryCode, x.EPF, x.FullName, x.IsActive }).ToListAsync(cancellationToken))
            .ToDictionary(x => Key(x.FactoryCode, x.EPF), StringComparer.OrdinalIgnoreCase);
        var activeByNumber = (await db.MobileAccounts.Include(x => x.Employee).Where(x => x.IsActive).ToListAsync(cancellationToken))
            .GroupBy(x => x.MobileNumber, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var errors = new List<ImportRowError>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int created = 0, updated = 0, unchanged = 0;
        foreach (var row in sheet.Rows)
        {
            var rowErrors = new List<string>();
            var number = Required(row.Text("Mobile Number"), "Mobile Number", 30, rowErrors);
            var factory = Required(row.Text("Factory Code"), "Factory Code", 50, rowErrors);
            var epf = Required(row.Text("EPF"), "EPF", 50, rowErrors);
            var creditLimit = Amount(row.Cell("Monthly Credit Limit"), "Monthly Credit Limit", rowErrors);
            var rental = Amount(row.Cell("Monthly Rental"), "Monthly Rental", rowErrors);
            Guid? employeeId = null;
            if (factory is not null && epf is not null)
            {
                if (!employees.TryGetValue(Key(factory, epf), out var employee)) rowErrors.Add($"No employee with Factory {factory} + EPF {epf}.");
                else if (!employee.IsActive) rowErrors.Add($"Employee {epf} ({employee.FullName}) in factory {factory} is inactive.");
                else employeeId = employee.Id;
            }
            if (number is not null)
            {
                if (seen.TryGetValue(number, out var firstRow)) rowErrors.Add($"Mobile number {number} also appears on row {firstRow}.");
                else seen[number] = row.Number;
            }
            MobileAccount? current = null;
            if (number is not null && employeeId is not null && activeByNumber.TryGetValue(number, out current))
            {
                if (current.Status == SimStatus.Pooled) rowErrors.Add($"Mobile number {number} is in the SIM Pool. Use Assign from Pool to give it to a new holder.");
                else if (current.EmployeeId != employeeId) rowErrors.Add($"Mobile number {number} is already allocated to EPF {current.Employee.EPF} ({current.Employee.FullName}). Use Reassign to move it.");
            }
            if (rowErrors.Count > 0) { errors.AddRange(rowErrors.Select(message => new ImportRowError(row.Number, message))); continue; }

            if (current is not null)
            {
                if (current.MonthlyCreditLimit == creditLimit && current.MonthlyRental == rental) { unchanged++; continue; }
                current.MonthlyCreditLimit = creditLimit!.Value; current.MonthlyRental = rental!.Value;
                current.UpdatedAtUtc = clock.UtcNow; current.UpdatedBy = currentUser.UserId;
                updated++;
            }
            else
            {
                db.MobileAccounts.Add(new MobileAccount { MobileNumber = number!, EmployeeId = employeeId!.Value, MonthlyCreditLimit = creditLimit!.Value, MonthlyRental = rental!.Value, CreatedAtUtc = clock.UtcNow, CreatedBy = currentUser.UserId });
                created++;
            }
        }
        return await FinishAsync(nameof(MobileAccount), "MobileAllocationsImported", sheet.Rows.Count, created, updated, unchanged, errors, commit, cancellationToken);
    }

    // Saves only when asked to and when every row is valid, with one audit entry for the import; otherwise discards the pending changes.
    private async Task<MasterDataImportResult> FinishAsync(string entityName, string action, int total, int created, int updated, int unchanged, List<ImportRowError> errors, bool commit, CancellationToken token)
    {
        var save = commit && errors.Count == 0;
        if (save)
        {
            var now = clock.UtcNow;
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = entityName, EntityId = Guid.NewGuid(), Action = action,
                BeforeDataJson = "{}", AfterDataJson = JsonSerializer.Serialize(new { Rows = total, New = created, Updated = updated, Unchanged = unchanged }),
                PerformedBy = currentUser.UserId, PerformedAt = now, CreatedAtUtc = now, CreatedBy = currentUser.UserId
            });
            await db.SaveChangesAsync(token);
        }
        else db.ChangeTracker.Clear();
        return new MasterDataImportResult(total, created, updated, unchanged, errors.OrderBy(error => error.Row).ToList(), save);
    }

    private static async Task<ImportSheet> ReadSheetAsync(Stream workbook, IReadOnlyCollection<string> requiredColumns, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        await workbook.CopyToAsync(buffer, token);
        buffer.Position = 0;
        XLWorkbook book;
        try { book = new XLWorkbook(buffer); }
        catch (Exception) { throw new MasterDataValidationException("The file is not a valid Excel workbook (.xlsx)."); }
        using (book)
        {
            var sheet = book.Worksheets.First();
            var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var headers = new Dictionary<string, int>();
            for (var column = 1; column <= lastColumn; column++)
            {
                var heading = Normalize(sheet.Cell(1, column).GetFormattedString());
                if (heading.Length > 0) headers.TryAdd(heading, column);
            }
            var missing = requiredColumns.Where(column => !headers.ContainsKey(Normalize(column))).ToList();
            if (missing.Count > 0) throw new MasterDataValidationException($"Missing column(s): {string.Join(", ", missing)}. Download the template to see the expected headings.");

            var rows = new List<ImportRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var number = 2; number <= lastRow; number++)
            {
                var cells = headers.ToDictionary(header => header.Key, header => CellValue.From(sheet.Cell(number, header.Value)));
                if (cells.Values.All(cell => cell.Text.Length == 0)) continue;
                rows.Add(new ImportRow(number, cells));
                if (rows.Count > MaxRows) throw new MasterDataValidationException($"The file has more than {MaxRows} rows. Split it into smaller files.");
            }
            if (rows.Count == 0) throw new MasterDataValidationException("The file has no data rows below the heading row.");
            return new ImportSheet(rows);
        }
    }

    private static byte[] Template(string sheetName, string[] columns, string[] required, string[] textColumns, string[] instructions)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add(sheetName);
        for (var index = 0; index < columns.Length; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = required.Contains(columns[index]) ? $"{columns[index]} *" : columns[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            cell.Style.Font.FontColor = XLColor.White;
            sheet.Column(index + 1).Width = 22;
            // Text format keeps leading zeros in EPF and mobile numbers.
            if (textColumns.Contains(columns[index])) sheet.Range(2, index + 1, MaxRows + 1, index + 1).Style.NumberFormat.Format = "@";
        }
        sheet.SheetView.FreezeRows(1);
        var help = book.Worksheets.Add("Instructions");
        help.Cell(1, 1).Value = "How to fill this template";
        help.Cell(1, 1).Style.Font.Bold = true;
        for (var index = 0; index < instructions.Length; index++) help.Cell(index + 3, 1).Value = $"• {instructions[index]}";
        help.Column(1).Width = 120;
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<Dictionary<string, string>> CodesAsync(IQueryable<string> codes, CancellationToken token) =>
        (await codes.ToListAsync(token)).ToDictionary(code => code, code => code, StringComparer.OrdinalIgnoreCase);

    // Required code that must be in the active master list; returns it as stored there (so "f01" becomes "F01").
    private static string? Code(string text, Dictionary<string, string> codes, string column, List<string> errors)
    {
        if (text.Length == 0) { errors.Add($"{column} is required."); return null; }
        if (codes.TryGetValue(text, out var code)) return code;
        errors.Add($"{column} '{text}' is not an active code.");
        return null;
    }

    private static string? Required(string text, string column, int maxLength, List<string> errors)
    {
        if (text.Length == 0) { errors.Add($"{column} is required."); return null; }
        return Optional(text, column, maxLength, errors);
    }

    private static string? Optional(string text, string column, int maxLength, List<string> errors)
    {
        if (text.Length == 0) return null;
        if (text.Length > maxLength) { errors.Add($"{column} is longer than {maxLength} characters."); return null; }
        return text;
    }

    private static decimal? Amount(CellValue cell, string column, List<string> errors)
    {
        if (cell.Text.Length == 0) { errors.Add($"{column} is required."); return null; }
        var amount = cell.Number ?? (decimal.TryParse(cell.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null);
        if (amount is null) { errors.Add($"{column} '{cell.Text}' is not a number."); return null; }
        if (amount < 0m) { errors.Add($"{column} cannot be negative."); return null; }
        if (decimal.Round(amount.Value, 2) != amount.Value || amount > 9999999999999999.99m) { errors.Add($"{column} must have at most two decimal places."); return null; }
        return amount;
    }

    private static string Key(string factory, string epf) => $"{factory.Trim()}|{epf.Trim()}";
    private static string Normalize(string heading) => new(heading.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private sealed record ImportSheet(List<ImportRow> Rows);

    private sealed record ImportRow(int Number, Dictionary<string, CellValue> Cells)
    {
        public CellValue Cell(string column) => Cells.TryGetValue(Normalize(column), out var cell) ? cell : CellValue.Empty;
        public string Text(string column) => Cell(column).Text;
    }

    private sealed record CellValue(string Text, decimal? Number)
    {
        public static readonly CellValue Empty = new(string.Empty, null);

        // Numbers typed into Excel (EPF 12345, mobile 771234567, amounts) arrive as numbers, not text.
        public static CellValue From(IXLCell cell)
        {
            var value = cell.Value;
            if (value.IsBlank) return Empty;
            if (value.IsNumber)
            {
                var number = (decimal)value.GetNumber();
                return new CellValue(number.ToString("0.##########", CultureInfo.InvariantCulture), number);
            }
            return new CellValue(cell.GetFormattedString().Trim(), null);
        }
    }
}
