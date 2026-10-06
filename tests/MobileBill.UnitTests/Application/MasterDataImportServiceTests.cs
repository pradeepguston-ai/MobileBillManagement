using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Application;

public sealed class MasterDataImportServiceTests
{
    private static readonly string[] EmployeeHeadings = ["Factory Code *", "EPF *", "Full Name *", "Calling Name", "Category Code *", "Designation Code *", "Department Code *", "Section Code", "Sub Section Code"];
    private static readonly string[] AllocationHeadings = ["Mobile Number *", "Factory Code *", "EPF *", "Monthly Credit Limit *", "Monthly Rental *"];

    [Fact]
    public async Task Checking_employees_saves_nothing_and_counts_new_updated_and_unchanged()
    {
        await using var db = await SeedAsync();
        var file = Workbook(EmployeeHeadings,
            ["F1", "100", "Existing Same", "", "C", "DS", "D", "", ""],
            ["F1", "101", "Existing Renamed", "Ren", "C", "DS", "D", "S1", "SS1"],
            ["F2", "100", "Same EPF Other Factory", "", "C", "DS", "D", "", ""]);

        var result = await Service(db).ImportEmployeesAsync(file, commit: false, default);

        Assert.Equal((3, 1, 1, 1, false), (result.TotalRows, result.NewCount, result.UpdatedCount, result.UnchangedCount, result.Committed));
        Assert.Empty(result.Errors);
        Assert.Equal(2, await db.Employees.CountAsync());
        Assert.Equal("Existing Two", (await db.Employees.SingleAsync(x => x.EPF == "101")).FullName);
    }

    [Fact]
    public async Task Importing_valid_employees_saves_them_and_writes_one_audit_entry()
    {
        await using var db = await SeedAsync();
        var file = Workbook(EmployeeHeadings,
            ["f1", "101", "Existing Renamed", "Ren", "c", "ds", "d", "s1", "ss1"],
            ["F2", "100", "Same EPF Other Factory", "", "C", "DS", "D", "", ""]);

        var result = await Service(db).ImportEmployeesAsync(file, commit: true, default);

        Assert.True(result.Committed);
        var renamed = await db.Employees.SingleAsync(x => x.EPF == "101");
        Assert.Equal(("Existing Renamed", "Ren", "S1", "SS1"), (renamed.FullName, renamed.CallingName, renamed.SectionCode, renamed.SubSectionCode));
        var twin = await db.Employees.SingleAsync(x => x.EPF == "100" && x.FactoryCode == "F2");
        Assert.Equal(("Same EPF Other Factory", "C"), (twin.FullName, twin.CategoryCode));
        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(("EmployeesImported", "importer"), (audit.Action, audit.PerformedBy));
    }

    [Fact]
    public async Task Any_invalid_employee_row_blocks_the_whole_file_and_reports_row_numbers()
    {
        await using var db = await SeedAsync();
        var file = Workbook(EmployeeHeadings,
            ["F1", "200", "Good Row", "", "C", "DS", "D", "", ""],
            ["NOPE", "201", "Bad Factory", "", "C", "DS", "D", "", ""],
            ["F1", "200", "Duplicate Of Row 2", "", "C", "DS", "D", "", ""],
            ["F1", "202", "Wrong Section", "", "C", "DS", "D2", "S1", ""],
            ["F1", "", "", "", "C", "DS", "D", "", "SS1"]);

        var result = await Service(db).ImportEmployeesAsync(file, commit: true, default);

        Assert.False(result.Committed);
        Assert.Contains(result.Errors, error => error.Row == 3 && error.Message.Contains("Factory Code 'NOPE'"));
        Assert.Contains(result.Errors, error => error.Row == 4 && error.Message.Contains("also appears on row 2"));
        Assert.Contains(result.Errors, error => error.Row == 5 && error.Message.Contains("does not belong to department"));
        Assert.Contains(result.Errors, error => error.Row == 6 && error.Message == "EPF is required.");
        Assert.Contains(result.Errors, error => error.Row == 6 && error.Message.Contains("needs a Section Code"));
        Assert.Equal(2, await db.Employees.CountAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Allocations_resolve_the_holder_by_factory_and_epf()
    {
        await using var db = await SeedAsync();
        db.Employees.Add(new Employee { EPF = "100", FullName = "Twin In F2", FactoryCode = "F2", CategoryCode = "C", DesignationCode = "DS", DepartmentCode = "D" });
        await db.SaveChangesAsync();
        var file = Workbook(AllocationHeadings, [771000001m, "F2", 100m, 1500m, 250.5m]);

        var result = await Service(db).ImportMobileAccountsAsync(file, commit: true, default);

        Assert.True(result.Committed);
        var allocation = await db.MobileAccounts.Include(x => x.Employee).SingleAsync();
        Assert.Equal(("771000001", "Twin In F2", 1500m, 250.5m), (allocation.MobileNumber, allocation.Employee.FullName, allocation.MonthlyCreditLimit, allocation.MonthlyRental));
    }

    [Fact]
    public async Task Allocation_rows_update_amounts_for_the_same_holder_and_reject_conflicts()
    {
        await using var db = await SeedAsync();
        var holder = await db.Employees.SingleAsync(x => x.EPF == "100");
        var inactive = new Employee { EPF = "300", FullName = "Left", FactoryCode = "F1", CategoryCode = "C", DesignationCode = "DS", DepartmentCode = "D", IsActive = false };
        db.AddRange(inactive,
            new MobileAccount { MobileNumber = "0771", EmployeeId = holder.Id, MonthlyCreditLimit = 1000m, MonthlyRental = 100m },
            new MobileAccount { MobileNumber = "0772", EmployeeId = holder.Id, MonthlyCreditLimit = 1000m, MonthlyRental = 100m },
            new MobileAccount { MobileNumber = "0773", EmployeeId = holder.Id, MonthlyCreditLimit = 1000m, MonthlyRental = 100m, Status = SimStatus.Pooled, PooledOn = new DateOnly(2026, 9, 1) });
        await db.SaveChangesAsync();
        var file = Workbook(AllocationHeadings,
            ["0771", "F1", "100", 2000m, 150m],
            ["0772", "F1", "101", 1000m, 100m],
            ["0773", "F1", "101", 1000m, 100m],
            ["0774", "F1", "300", 1000m, 100m],
            ["0775", "F1", "101", -5m, 1.005m]);

        var check = await Service(db).ImportMobileAccountsAsync(file, commit: false, default);

        Assert.Contains(check.Errors, error => error.Row == 3 && error.Message.Contains("already allocated to EPF 100"));
        Assert.Contains(check.Errors, error => error.Row == 4 && error.Message.Contains("SIM Pool"));
        Assert.Contains(check.Errors, error => error.Row == 5 && error.Message.Contains("inactive"));
        Assert.Contains(check.Errors, error => error.Row == 6 && error.Message.Contains("cannot be negative"));
        Assert.Contains(check.Errors, error => error.Row == 6 && error.Message.Contains("two decimal places"));
        Assert.Equal(1, check.UpdatedCount);

        var valid = await Service(db).ImportMobileAccountsAsync(Workbook(AllocationHeadings, ["0771", "F1", "100", 2000m, 150m]), commit: true, default);
        Assert.Equal((1, true), (valid.UpdatedCount, valid.Committed));
        Assert.Equal(2000m, (await db.MobileAccounts.SingleAsync(x => x.MobileNumber == "0771")).MonthlyCreditLimit);
    }

    [Fact]
    public async Task File_without_the_required_columns_is_rejected_before_any_row_is_read()
    {
        await using var db = await SeedAsync();
        var file = Workbook(["Mobile Number", "EPF"], ["0771", "100"]);

        var error = await Assert.ThrowsAsync<MasterDataValidationException>(() => Service(db).ImportMobileAccountsAsync(file, commit: false, default));

        Assert.Contains("Factory Code", error.Message);
        Assert.Contains("Monthly Credit Limit", error.Message);
    }

    [Fact]
    public async Task Templates_have_the_expected_headings()
    {
        await using var db = await SeedAsync();
        using var employees = new XLWorkbook(new MemoryStream(Service(db).EmployeeTemplate().Content));
        using var allocations = new XLWorkbook(new MemoryStream(Service(db).MobileAccountTemplate().Content));

        Assert.Equal(EmployeeHeadings, employees.Worksheet(1).Row(1).CellsUsed().Select(cell => cell.GetString()));
        Assert.Equal(AllocationHeadings, allocations.Worksheet(1).Row(1).CellsUsed().Select(cell => cell.GetString()));
    }

    private static ClosedXmlMasterDataImportService Service(MobileBillDbContext db) => new(db, new TestUser(), new TestClock());

    private static MemoryStream Workbook(string[] headings, params object[][] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("Data");
        for (var column = 0; column < headings.Length; column++) sheet.Cell(1, column + 1).Value = headings[column];
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                sheet.Cell(row + 2, column + 1).Value = rows[row][column] switch { decimal number => number, string text => text, _ => Blank.Value };
        var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static async Task<MobileBillDbContext> SeedAsync()
    {
        var db = new MobileBillDbContext(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.AddRange(
            new Factory { Code = "F1", Name = "Factory One" }, new Factory { Code = "F2", Name = "Factory Two" },
            new EmployeeCategory { Code = "C", Name = "Category" }, new Designation { Code = "DS", Name = "Designation" },
            new Department { Code = "D", Name = "Department" }, new Department { Code = "D2", Name = "Other Department" },
            new Section { Code = "S1", Name = "Section", DepartmentCode = "D" }, new SubSection { Code = "SS1", Name = "Sub Section", SectionCode = "S1" },
            new Employee { EPF = "100", FullName = "Existing Same", FactoryCode = "F1", CategoryCode = "C", DesignationCode = "DS", DepartmentCode = "D" },
            new Employee { EPF = "101", FullName = "Existing Two", FactoryCode = "F1", CategoryCode = "C", DesignationCode = "DS", DepartmentCode = "D" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private sealed class TestUser : ICurrentUserService { public string UserId => "importer"; public string DisplayName => "Importer"; public UserRole Role => UserRole.ITEngineer; }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero); }
}
