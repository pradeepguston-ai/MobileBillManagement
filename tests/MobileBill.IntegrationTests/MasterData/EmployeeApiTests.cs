using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.MasterData;

public sealed class EmployeeApiTests
{
    [Fact]
    public async Task Employee_list_query_executes_on_a_relational_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseSqlite(connection));
            });
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            await CreateEmployeeTablesAsync(connection);
            var createdAt = DateTimeOffset.UnixEpoch;
            var factoryRecord = new Factory { Code = "HQ", Name = "Head Office", CreatedAtUtc = createdAt };
            var department = new Department { Code = "IT", Name = "Information Technology", CreatedAtUtc = createdAt };
            var category = new EmployeeCategory { Code = "STAFF", Name = "Staff", CreatedAtUtc = createdAt };
            var designation = new Designation { Code = "ENG", Name = "Engineer", CreatedAtUtc = createdAt };
            db.AddRange(factoryRecord, department, category, designation);
            await db.SaveChangesAsync();
            db.Employees.Add(new Employee
            {
                EPF = "EPF-001",
                FullName = "Test Employee",
                CallingName = "Test",
                Gender = Gender.Female,
                CategoryCode = category.Code,
                DesignationCode = designation.Code,
                FactoryCode = factoryRecord.Code,
                DepartmentCode = department.Code,
                CreatedAtUtc = createdAt
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);
        var response = await client.GetAsync("/api/employees?pageNumber=1&pageSize=20&search=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EmployeeDto>>();
        var employee = Assert.Single(result!.Items);
        Assert.Equal("EPF-001", employee.Epf);
        Assert.Equal("Information Technology", employee.DepartmentName);
        Assert.Equal((Gender.Female, "MS."), (employee.Gender, employee.Title));
    }

    [Fact]
    public async Task Mobile_account_list_query_executes_on_a_relational_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using var factory = CreateApiFactory(connection);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            await CreateEmployeeTablesAsync(connection);
            var employee = await SeedEmployeeAsync(db);
            db.MobileAccounts.Add(new MobileAccount
            {
                MobileNumber = "0771234567",
                EmployeeId = employee.Id,
                CreatedAtUtc = DateTimeOffset.UnixEpoch
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);
        var response = await client.GetAsync("/api/mobile-accounts?pageNumber=1&pageSize=20&search=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MobileAccountDto>>();
        var allocation = Assert.Single(result!.Items);
        Assert.Equal("0771234567", allocation.MobileNumber);
        Assert.Equal("EPF-001", allocation.Epf);
        Assert.Equal("Head Office", allocation.Factory);
        Assert.Equal("Information Technology", allocation.Department);
    }

    [Fact]
    public async Task Creating_mobile_allocation_saves_employee_and_both_monthly_amounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = CreateApiFactory(connection);
        Guid employeeId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            await CreateEmployeeTablesAsync(connection);
            employeeId = (await SeedEmployeeAsync(db)).Id;
        }
        var packageId = Guid.NewGuid();
        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO MobilePackages (Id, Code, ProviderId, Description, MonthlyRental, TotalWithTax, DefaultCreditLimit, IsActive) VALUES ($id, 'PPU23_700', $provider, 'Package', 700, 940, 1000, 1)";
            insert.Parameters.AddWithValue("$id", packageId.ToString().ToUpperInvariant());
            insert.Parameters.AddWithValue("$provider", Guid.NewGuid().ToString().ToUpperInvariant());
            await insert.ExecuteNonQueryAsync();
        }

        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);
        var withoutPackage = await client.PostAsJsonAsync("/api/mobile-accounts", new
        {
            mobileNumber = "0771234567", employeeId, monthlyCreditLimit = 1500.25m, monthlyRental = 700m
        });
        var response = await client.PostAsJsonAsync("/api/mobile-accounts", new
        {
            mobileNumber = "0771234567", employeeId, monthlyCreditLimit = 1500.25m, monthlyRental = 700m, packageId
        });

        Assert.Equal(HttpStatusCode.BadRequest, withoutPackage.StatusCode);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var allocation = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("EPF-001", allocation.GetProperty("epf").GetString());
        Assert.Equal(1500.25m, allocation.GetProperty("monthlyCreditLimit").GetDecimal());
        Assert.Equal(700m, allocation.GetProperty("monthlyRental").GetDecimal());
        Assert.Equal("PPU23_700", allocation.GetProperty("packageCode").GetString());
    }

    [Fact]
    public async Task Creating_mobile_allocation_rejects_negative_monthly_amounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = CreateApiFactory(connection);
        Guid employeeId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            await CreateEmployeeTablesAsync(connection);
            employeeId = (await SeedEmployeeAsync(db)).Id;
        }

        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);
        var response = await client.PostAsJsonAsync("/api/mobile-accounts", new
        {
            mobileNumber = "0771234567", employeeId, monthlyCreditLimit = -1m, monthlyRental = 700m
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_mobile_allocation_requires_both_monthly_amounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var factory = CreateApiFactory(connection);
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);

        var missingCredit = await client.PostAsJsonAsync("/api/mobile-accounts", new
        {
            mobileNumber = "0771234567", employeeId = Guid.NewGuid(), monthlyRental = 700m
        });
        var missingRental = await client.PostAsJsonAsync("/api/mobile-accounts", new
        {
            mobileNumber = "0771234567", employeeId = Guid.NewGuid(), monthlyCreditLimit = 1500m
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingCredit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingRental.StatusCode);
    }

    [Fact]
    public async Task Creating_department_returns_created_after_the_record_is_persisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using var factory = CreateApiFactory(connection);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await CreateEmployeeTablesAsync(connection);
        }

        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);
        var response = await client.PostAsJsonAsync("/api/departments", new
        {
            code = "FIN",
            name = "Finance"
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var department = await response.Content.ReadFromJsonAsync<ReferenceDataDto>();
        Assert.NotNull(department);
        Assert.Equal("FIN", department.Code);
    }

    [Fact]
    public async Task Creating_employee_returns_created_after_the_record_is_persisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using var factory = CreateApiFactory(connection);
        string categoryCode;
        string designationCode;
        string factoryCode;
        string departmentCode;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            await CreateEmployeeTablesAsync(connection);
            var createdAt = DateTimeOffset.UnixEpoch;
            var factoryRecord = new Factory { Code = "HQ", Name = "Head Office", CreatedAtUtc = createdAt };
            var department = new Department { Code = "IT", Name = "Information Technology", CreatedAtUtc = createdAt };
            var category = new EmployeeCategory { Code = "STAFF", Name = "Staff", CreatedAtUtc = createdAt };
            var designation = new Designation { Code = "ENG", Name = "Engineer", CreatedAtUtc = createdAt };
            db.AddRange(factoryRecord, department, category, designation);
            await db.SaveChangesAsync();
            factoryCode = factoryRecord.Code;
            departmentCode = department.Code;
            categoryCode = category.Code;
            designationCode = designation.Code;
        }

        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);
        var response = await client.PostAsJsonAsync("/api/employees", new
        {
            epf = "EPF-002",
            fullName = "Second Employee",
            callingName = "Second",
            gender = "Female",
            categoryCode,
            designationCode,
            factoryCode,
            departmentCode
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var employee = await response.Content.ReadFromJsonAsync<EmployeeDto>();
        Assert.NotNull(employee);
        Assert.Equal("EPF-002", employee.Epf);
        Assert.Equal("Head Office", employee.FactoryName);
        Assert.Equal("Information Technology", employee.DepartmentName);
        Assert.Equal((Gender.Female, "MS."), (employee.Gender, employee.Title));
    }

    [Fact]
    public async Task Mobile_account_can_be_read_by_id_on_a_relational_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using var factory = CreateApiFactory(connection);
        Guid accountId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            await CreateEmployeeTablesAsync(connection);
            var employee = await SeedEmployeeAsync(db);
            var account = new MobileAccount
            {
                MobileNumber = "0777654321",
                EmployeeId = employee.Id,
                CreatedAtUtc = DateTimeOffset.UnixEpoch
            };
            db.MobileAccounts.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
        }

        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);
        var response = await client.GetAsync($"/api/mobile-accounts/{accountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Allocation_excel_report_has_a_sheet_each_for_assigned_pooled_and_disconnected_numbers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using var factory = CreateApiFactory(connection);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            await CreateEmployeeTablesAsync(connection);
            var employee = await SeedEmployeeAsync(db);
            db.MobileAccounts.AddRange(
                new MobileAccount { MobileNumber = "0771111111", EmployeeId = employee.Id, MonthlyCreditLimit = 1500m, MonthlyRental = 700m, SimType = SimType.VoiceData, CreatedAtUtc = DateTimeOffset.UnixEpoch },
                new MobileAccount { MobileNumber = "0772222222", EmployeeId = employee.Id, Status = SimStatus.Pooled, PooledOn = new DateOnly(2026, 10, 1), StatusReason = "Resigned", CreatedAtUtc = DateTimeOffset.UnixEpoch },
                new MobileAccount { MobileNumber = "0773333333", EmployeeId = employee.Id, Status = SimStatus.Disconnected, IsActive = false, DisconnectedOn = new DateOnly(2026, 9, 15), StatusReason = "Lost phone", CreatedAtUtc = DateTimeOffset.UnixEpoch });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        // A read-only report, so a Viewer can download it.
        TestAuth.Authorize(client, UserRole.Viewer);
        var response = await client.GetAsync("/api/mobile-accounts/export/excel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Mobile_Allocations_", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        using var workbook = new ClosedXML.Excel.XLWorkbook(await response.Content.ReadAsStreamAsync());
        Assert.Equal(["Allocations", "SIM Pool", "Disconnected"], workbook.Worksheets.Select(sheet => sheet.Name));

        var allocations = workbook.Worksheet("Allocations");
        Assert.Equal("Mobile Number", allocations.Cell(5, 1).GetString());
        Assert.Equal(("0771111111", "EPF-001", "Test Employee", "Test", "Head Office", "Information Technology", "Voice + Data"),
            (allocations.Cell(6, 1).GetString(), allocations.Cell(6, 2).GetString(), allocations.Cell(6, 3).GetString(), allocations.Cell(6, 4).GetString(), allocations.Cell(6, 5).GetString(), allocations.Cell(6, 6).GetString(), allocations.Cell(6, 7).GetString()));
        Assert.Equal(1500m, allocations.Cell(6, 9).GetValue<decimal>());
        Assert.True(allocations.Cell(7, 1).IsEmpty());

        var pool = workbook.Worksheet("SIM Pool");
        Assert.Equal(("0772222222", "Resigned"), (pool.Cell(6, 1).GetString(), pool.Cell(6, 9).GetString()));
        Assert.Equal(new DateTime(2026, 10, 1), pool.Cell(6, 7).GetDateTime());
        Assert.True(pool.Cell(7, 1).IsEmpty());

        var disconnected = workbook.Worksheet("Disconnected");
        Assert.Equal("Reason for Disconnection", disconnected.Cell(5, 9).GetString());
        Assert.Equal(("0773333333", "Lost phone"), (disconnected.Cell(6, 1).GetString(), disconnected.Cell(6, 9).GetString()));
        Assert.Equal(new DateTime(2026, 9, 15), disconnected.Cell(6, 8).GetDateTime());
        Assert.True(disconnected.Cell(7, 1).IsEmpty());
    }

    private static WebApplicationFactory<Program> CreateApiFactory(SqliteConnection connection) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseSqlite(connection));
            });
        });

    private static async Task<Employee> SeedEmployeeAsync(MobileBillDbContext db)
    {
        var createdAt = DateTimeOffset.UnixEpoch;
        var factoryRecord = new Factory { Code = "HQ", Name = "Head Office", CreatedAtUtc = createdAt };
        var department = new Department { Code = "IT", Name = "Information Technology", CreatedAtUtc = createdAt };
        var category = new EmployeeCategory { Code = "STAFF", Name = "Staff", CreatedAtUtc = createdAt };
        var designation = new Designation { Code = "ENG", Name = "Engineer", CreatedAtUtc = createdAt };
        db.AddRange(factoryRecord, department, category, designation);
        await db.SaveChangesAsync();
        var employee = new Employee
        {
            EPF = "EPF-001", FullName = "Test Employee", CallingName = "Test",
            CategoryCode = category.Code, DesignationCode = designation.Code, FactoryCode = factoryRecord.Code,
            DepartmentCode = department.Code, CreatedAtUtc = createdAt
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private static async Task CreateEmployeeTablesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Factories (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL, Name TEXT NOT NULL, IsActive INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE Departments (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL, Name TEXT NOT NULL, IsActive INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE EmployeeCategories (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL, Name TEXT NOT NULL, IsActive INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE Designations (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL, Name TEXT NOT NULL, IsActive INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE MobileAccounts (
                Id TEXT PRIMARY KEY, MobileNumber TEXT NOT NULL, EmployeeId TEXT NOT NULL, MonthlyCreditLimit NUMERIC NOT NULL, MonthlyRental NUMERIC NOT NULL, IsActive INTEGER NOT NULL,
                Status TEXT NOT NULL DEFAULT 'Assigned', PooledOn TEXT NULL, DisconnectedOn TEXT NULL, StatusReason TEXT NULL, PackageId TEXT NULL, SimType TEXT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE MobileDevices (
                Id TEXT PRIMARY KEY, AssetTag TEXT NOT NULL, Imei1 TEXT NOT NULL, Imei2 TEXT NULL, Brand TEXT NOT NULL, Model TEXT NOT NULL, SerialNumber TEXT NULL,
                PurchaseDate TEXT NOT NULL, PurchaseCost NUMERIC NOT NULL, WarrantyUntil TEXT NULL, Supplier TEXT NULL, Notes TEXT NULL, Status TEXT NOT NULL,
                StatusSince TEXT NOT NULL, CurrentEmployeeId TEXT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE MobilePackages (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL, ProviderId TEXT NOT NULL, Description TEXT NOT NULL, MonthlyRental NUMERIC NOT NULL, TotalWithTax NUMERIC NOT NULL,
                DefaultCreditLimit NUMERIC NOT NULL, IsActive INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE Employees (
                Id TEXT PRIMARY KEY, EPF TEXT NOT NULL, FullName TEXT NOT NULL, CallingName TEXT NULL, NameWithInitials TEXT NULL, Gender TEXT NULL,
                CategoryCode TEXT NOT NULL, DesignationCode TEXT NOT NULL, FactoryCode TEXT NOT NULL, DepartmentCode TEXT NOT NULL,
                SectionCode TEXT NULL, SubSectionCode TEXT NULL, ThirdPartyCompanyCode TEXT NULL, DefaultResponsibility TEXT NULL, ResignedOn TEXT NULL, ResignationReason TEXT NULL,
                IsActive INTEGER NOT NULL, CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE Sections (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL, Name TEXT NOT NULL, DepartmentCode TEXT NOT NULL, IsActive INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE SubSections (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL, Name TEXT NOT NULL, SectionCode TEXT NOT NULL, IsActive INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            CREATE TABLE ThirdPartyCompanies (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL, Name TEXT NOT NULL, IsActive INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00+00:00', CreatedBy TEXT NULL, UpdatedAtUtc TEXT NULL, UpdatedBy TEXT NULL);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
