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
    private static readonly string[] EmployeeHeadings = ["Factory Code *", "EPF *", "Full Name *", "Calling Name", "Gender *", "Category *", "Designation *", "Department *", "Section", "Sub Section"];
    // Files made before the columns took names: the same columns with "Code" headings still read.
    private static readonly string[] OldEmployeeHeadings = ["Factory Code *", "EPF *", "Full Name *", "Calling Name", "Gender *", "Category Code *", "Designation Code *", "Department Code *", "Section Code", "Sub Section Code"];
    private static readonly string[] AllocationHeadings = ["Mobile Number *", "Factory Code *", "EPF *", "Package Code *", "SIM Type", "Monthly Credit Limit *", "Monthly Rental *"];
    // Files made before Package Code was added still import for numbers that are already allocated.
    private static readonly string[] LegacyAllocationHeadings = ["Mobile Number *", "Factory Code *", "EPF *", "Monthly Credit Limit *", "Monthly Rental *"];

    [Fact]
    public async Task Checking_employees_saves_nothing_and_counts_new_updated_and_unchanged()
    {
        await using var db = await SeedAsync();
        var file = Workbook(EmployeeHeadings,
            ["F1", "100", "Existing Same", "", "Male", "C", "DS", "D", "", ""],
            ["F1", "101", "Existing Renamed", "Ren", "Male", "C", "DS", "D", "S1", "SS1"],
            ["F2", "100", "Same EPF Other Factory", "", "Male", "C", "DS", "D", "", ""]);

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
            ["f1", "101", "Existing Renamed", "Ren", "Male", "c", "ds", "d", "s1", "ss1"],
            ["F2", "100", "Same EPF Other Factory", "", "Male", "C", "DS", "D", "", ""]);

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
            ["F1", "200", "Good Row", "", "Male", "C", "DS", "D", "", ""],
            ["NOPE", "201", "Bad Factory", "", "Male", "C", "DS", "D", "", ""],
            ["F1", "200", "Duplicate Of Row 2", "", "Male", "C", "DS", "D", "", ""],
            ["F1", "202", "Wrong Section", "", "Male", "C", "DS", "D2", "S1", ""],
            ["F1", "", "", "", "Male", "C", "DS", "D", "", "SS1"]);

        var result = await Service(db).ImportEmployeesAsync(file, commit: true, default);

        Assert.False(result.Committed);
        Assert.Contains(result.Errors, error => error.Row == 3 && error.Message.Contains("Factory Code 'NOPE'"));
        Assert.Contains(result.Errors, error => error.Row == 4 && error.Message.Contains("also appears on row 2"));
        Assert.Contains(result.Errors, error => error.Row == 5 && error.Message == "Section 'S1' is not an active section in department 'Other Department'.");
        Assert.Contains(result.Errors, error => error.Row == 6 && error.Message == "EPF is required.");
        Assert.Contains(result.Errors, error => error.Row == 6 && error.Message == "Sub Section needs a Section.");
        Assert.Equal(2, await db.Employees.CountAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Category_designation_department_and_sections_are_written_by_name_and_old_code_headings_still_read()
    {
        await using var db = await SeedAsync();
        db.AddRange(new Section { Code = "SEC002", Name = "Section", DepartmentCode = "D2" }, new Department { Code = "DEP009", Name = "Information Technology" });
        await db.SaveChangesAsync();
        var byName = Workbook(EmployeeHeadings,
            ["F1", "300", "By Name", "", "Male", "category", "Designation", "  information   technology ", "", ""],
            ["F1", "301", "Same Section Name", "", "Male", "Category", "Designation", "Other Department", "Section", ""],
            ["F1", "302", "Unknown", "", "Male", "Category", "Pilot", "Department", "Section", "Sub Section"]);

        var result = await Service(db).ImportEmployeesAsync(byName, commit: false, default);

        Assert.Equal([(4, "Designation 'Pilot' is not an active designation.")], result.Errors.Select(error => (error.Row, error.Message)));
        var saved = await Service(db).ImportEmployeesAsync(Workbook(EmployeeHeadings,
            ["F1", "300", "By Name", "", "Male", "category", "Designation", "  information   technology ", "", ""],
            ["F1", "301", "Same Section Name", "", "Male", "Category", "Designation", "Other Department", "Section", ""],
            ["F1", "303", "Sub Section", "", "Male", "Category", "Designation", "Department", "Section", "Sub Section"]), commit: true, default);
        Assert.True(saved.Committed);
        var byNameEmployee = await db.Employees.AsNoTracking().SingleAsync(x => x.EPF == "300");
        Assert.Equal(("C", "DS", "DEP009"), (byNameEmployee.CategoryCode, byNameEmployee.DesignationCode, byNameEmployee.DepartmentCode));
        Assert.Equal("SEC002", (await db.Employees.AsNoTracking().SingleAsync(x => x.EPF == "301")).SectionCode);   // the "Section" of Other Department
        var withSub = await db.Employees.AsNoTracking().SingleAsync(x => x.EPF == "303");
        Assert.Equal(("S1", "SS1"), (withSub.SectionCode, withSub.SubSectionCode));

        var oldFile = Workbook(OldEmployeeHeadings, ["F2", "304", "Old Headings", "", "M", "C", "Designation", "D", "S1", ""]);
        var old = await Service(db).ImportEmployeesAsync(oldFile, commit: false, default);
        Assert.Equal((1, 0), (old.NewCount, old.Errors.Count));
    }

    [Fact]
    public async Task Name_with_initials_is_optional_and_imported_when_given()
    {
        await using var db = await SeedAsync();
        string[] headings = ["Factory Code", "EPF", "Full Name", "Name With Initials", "Gender", "Category", "Designation", "Department"];
        var file = Workbook(headings,
            ["F1", "500", "Sunil Mahinda Perera", "S. M. PERERA", "Male", "C", "DS", "D"],
            ["F1", "100", "Existing Same", "E. SAME", "Male", "C", "DS", "D"],
            ["F1", "501", "No Initials Given", "", "Female", "C", "DS", "D"]);

        var result = await Service(db).ImportEmployeesAsync(file, commit: true, default);

        Assert.Equal((2, 1, 0), (result.NewCount, result.UpdatedCount, result.Errors.Count));   // adding initials counts as an update
        var initials = await db.Employees.AsNoTracking().Where(x => x.FactoryCode == "F1").ToDictionaryAsync(x => x.EPF, x => x.NameWithInitials);
        Assert.Equal(("S. M. PERERA", "E. SAME", null), (initials["500"], initials["100"], initials["501"]));
    }

    [Fact]
    public async Task Third_party_allocation_is_imported_by_name_and_only_for_the_third_party_category()
    {
        await using var db = await SeedAsync();
        db.AddRange(new EmployeeCategory { Code = "TP", Name = "OTHER - THIRD-PARTY" }, new ThirdPartyCompany { Code = "TPC001", Name = "AVENGERS SECURITY & INVESTIGATIONS (PVT) LTD" });
        await db.SaveChangesAsync();
        string[] headings = ["Factory Code", "EPF", "Full Name", "Gender", "Category", "Designation", "Department", "Third Party Allocation"];
        var file = Workbook(headings,
            ["F1", "SEC_01", "Guard", "Male", "other - third-party", "DS", "D", "avengers security & investigations (pvt) ltd"],
            ["F1", "SEC_02", "Own Guard", "Male", "other - third-party", "DS", "D", "Not Applicable"],
            ["F1", "600", "Clerk", "Female", "C", "DS", "D", "AVENGERS SECURITY & INVESTIGATIONS (PVT) LTD"],
            ["F1", "SEC_03", "Guard", "Male", "other - third-party", "DS", "D", "Unknown Agency"]);

        var check = await Service(db).ImportEmployeesAsync(file, commit: false, default);

        Assert.Equal([(4, "Third Party Allocation is only for the OTHER - THIRD-PARTY category."), (5, "Third Party Allocation 'Unknown Agency' is not an active third party allocation.")],
            check.Errors.Select(error => (error.Row, error.Message)));
        var saved = await Service(db).ImportEmployeesAsync(Workbook(headings,
            ["F1", "SEC_01", "Guard", "Male", "other - third-party", "DS", "D", "avengers security & investigations (pvt) ltd"],
            ["F1", "SEC_02", "Own Guard", "Male", "other - third-party", "DS", "D", "Not Applicable"]), commit: true, default);
        Assert.True(saved.Committed);
        var companies = await db.Employees.AsNoTracking().Where(x => x.CategoryCode == "TP").ToDictionaryAsync(x => x.EPF, x => x.ThirdPartyCompanyCode);
        Assert.Equal(("TPC001", null), (companies["SEC_01"], companies["SEC_02"]));
    }

    [Fact]
    public async Task Gender_is_required_and_read_however_it_is_written()
    {
        await using var db = await SeedAsync();
        var file = Workbook(EmployeeHeadings,
            ["F1", "400", "Female Short", "", "f", "C", "DS", "D", "", ""],
            ["F1", "401", "By Title", "", "Mr.", "C", "DS", "D", "", ""],
            ["F1", "402", "No Gender", "", "", "C", "DS", "D", "", ""],
            ["F1", "403", "Unknown Gender", "", "Other", "C", "DS", "D", "", ""]);

        var check = await Service(db).ImportEmployeesAsync(file, commit: false, default);

        Assert.Equal([(4, "Gender is required."), (5, "Gender 'Other' is not Male or Female.")], check.Errors.Select(error => (error.Row, error.Message)));
        var saved = await Service(db).ImportEmployeesAsync(Workbook(EmployeeHeadings,
            ["F1", "400", "Female Short", "", "f", "C", "DS", "D", "", ""],
            ["F1", "401", "By Title", "", "Mr.", "C", "DS", "D", "", ""],
            ["F1", "100", "Existing Same", "", "FEMALE", "C", "DS", "D", "", ""]), commit: true, default);
        Assert.Equal((2, 1), (saved.NewCount, saved.UpdatedCount));   // a gender change counts as an update
        var genders = await db.Employees.AsNoTracking().Where(x => x.FactoryCode == "F1").ToDictionaryAsync(x => x.EPF, x => x.Gender);
        Assert.Equal((Gender.Female, Gender.Male, Gender.Female), (genders["400"], genders["401"], genders["100"]));
    }

    [Fact]
    public async Task Allocations_resolve_the_holder_by_factory_and_epf()
    {
        await using var db = await SeedAsync();
        db.Employees.Add(new Employee { EPF = "100", FullName = "Twin In F2", FactoryCode = "F2", CategoryCode = "C", DesignationCode = "DS", DepartmentCode = "D" });
        await db.SaveChangesAsync();
        var file = Workbook(AllocationHeadings, [771000001m, "F2", 100m, "PPU23_700", "voice and data", 1500m, 250.5m]);

        var result = await Service(db).ImportMobileAccountsAsync(file, commit: true, default);

        Assert.True(result.Committed);
        var allocation = await db.MobileAccounts.Include(x => x.Employee).SingleAsync();
        Assert.Equal(("771000001", "Twin In F2", 1500m, 250.5m, TestPackages.PackageId), (allocation.MobileNumber, allocation.Employee.FullName, allocation.MonthlyCreditLimit, allocation.MonthlyRental, allocation.PackageId!.Value));
        Assert.Equal(SimType.VoiceData, allocation.SimType);
    }

    [Theory]
    [InlineData("Voice", SimType.Voice)]
    [InlineData("Voice + Data", SimType.VoiceData)]
    [InlineData("DATA", SimType.Data)]
    [InlineData("eSIM", SimType.ESim)]
    [InlineData("E-SIM", SimType.ESim)]
    public async Task Sim_type_is_read_however_it_is_written(string text, SimType expected)
    {
        await using var db = await SeedAsync();
        var file = Workbook(AllocationHeadings, ["0790", "F1", "100", "PPU23_700", text, 1000m, 100m]);

        Assert.True((await Service(db).ImportMobileAccountsAsync(file, commit: true, default)).Committed);
        Assert.Equal(expected, (await db.MobileAccounts.SingleAsync()).SimType);
    }

    [Fact]
    public async Task An_unknown_sim_type_is_rejected_and_a_blank_one_keeps_the_existing_type()
    {
        await using var db = await SeedAsync();
        var holder = await db.Employees.SingleAsync(x => x.EPF == "100");
        db.Add(new MobileAccount { MobileNumber = "0791", EmployeeId = holder.Id, MonthlyCreditLimit = 1000m, MonthlyRental = 100m, SimType = SimType.Data, PackageId = TestPackages.PackageId });
        await db.SaveChangesAsync();

        var bad = await Service(db).ImportMobileAccountsAsync(Workbook(AllocationHeadings, ["0792", "F1", "100", "PPU23_700", "4G", 1000m, 100m]), commit: false, default);
        var blank = await Service(db).ImportMobileAccountsAsync(Workbook(AllocationHeadings, ["0791", "F1", "100", "", "", 1500m, 100m]), commit: true, default);

        Assert.Contains(bad.Errors, error => error.Message == "SIM Type '4G' is not one of Voice, Voice + Data, Data or eSIM.");
        Assert.True(blank.Committed);
        Assert.Equal((SimType.Data, 1500m), ((await db.MobileAccounts.SingleAsync()).SimType!.Value, (await db.MobileAccounts.SingleAsync()).MonthlyCreditLimit));
    }

    [Fact]
    public async Task A_new_allocation_needs_an_active_package_code()
    {
        await using var db = await SeedAsync();
        var file = Workbook(AllocationHeadings,
            ["0781", "F1", "100", "", "", 1000m, 100m],
            ["0782", "F1", "100", "NOPE", "", 1000m, 100m],
            ["0783", "F1", "100", "ppu23_700", "", 1000m, 100m]);

        var check = await Service(db).ImportMobileAccountsAsync(file, commit: false, default);

        Assert.Contains(check.Errors, error => error.Row == 2 && error.Message == "Package Code is required for a new allocation.");
        Assert.Contains(check.Errors, error => error.Row == 3 && error.Message.Contains("'NOPE' is not an active package"));
        Assert.DoesNotContain(check.Errors, error => error.Row == 4);
        Assert.Equal(1, check.NewCount);
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
        var file = Workbook(LegacyAllocationHeadings,
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

        var valid = await Service(db).ImportMobileAccountsAsync(Workbook(LegacyAllocationHeadings, ["0771", "F1", "100", 2000m, 150m]), commit: true, default);
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

        Assert.Equal(["Factory Code *", "EPF *", "Full Name *", "Name With Initials", "Calling Name", "Gender *", "Category *", "Designation *", "Department *", "Section", "Sub Section", "Third Party Allocation"], employees.Worksheet(1).Row(1).CellsUsed().Select(cell => cell.GetString()));
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
            new Employee { EPF = "100", FullName = "Existing Same", Gender = Gender.Male, FactoryCode = "F1", CategoryCode = "C", DesignationCode = "DS", DepartmentCode = "D" },
            new Employee { EPF = "101", FullName = "Existing Two", Gender = Gender.Male, FactoryCode = "F1", CategoryCode = "C", DesignationCode = "DS", DepartmentCode = "D" });
        TestPackages.Add(db);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private sealed class TestUser : ICurrentUserService { public string UserId => "importer"; public string DisplayName => "Importer"; public UserRole Role => UserRole.ITEngineer; }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero); }
}
