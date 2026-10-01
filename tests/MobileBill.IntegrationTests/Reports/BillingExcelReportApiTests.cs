using System.Net;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Reports;

public sealed class BillingExcelReportApiTests
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly DateTimeOffset GeneratedAt = new(2026, 9, 8, 5, 30, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedHeaders =
    [
        "Serial", "Mobile Phone", "EPF", "Name", "Category", "Designation", "Factory", "Department",
        "Calling Name", "Monthly Credit Limit", "Monthly Rental", "Actual Bill", "Variance", "Deduction",
        "Deduction Responsibility", "Remark"
    ];

    [Theory]
    [InlineData(BillBatchStatus.Draft)]
    [InlineData(BillBatchStatus.Uploaded)]
    [InlineData(BillBatchStatus.Parsed)]
    [InlineData(BillBatchStatus.Validated)]
    [InlineData(BillBatchStatus.ITReview)]
    [InlineData(BillBatchStatus.HRApproval)]
    [InlineData(BillBatchStatus.FinanceApproval)]
    public async Task Non_completed_batch_cannot_be_exported(BillBatchStatus status)
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(status, [ValidRow()], 100m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(BillBatchStatus.Completed)]
    [InlineData(BillBatchStatus.Locked)]
    public async Task Completed_and_locked_batches_can_be_exported(BillBatchStatus status)
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(status, [ValidRow(actualBill: 0m, finalDeduction: 0m)], 0m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Export_uses_stored_values_and_reconciles_excluded_rows()
    {
        using var fixture = new ReportApiFixture();
        var rows = new[]
        {
            ValidRow("761499198", Responsibility.ByUser, 120.25m, 100m, 20m, -999.99m, 0m, "Approved waiver"),
            ValidRow("761499199", Responsibility.ByCompany, 80m, 40m, 10m, 77.77m, 0m, "Company charge"),
            ValidRow("768791861", null, 5m, status: MonthlyBillStatus.Excluded, assessed: false)
        };
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, rows, 205.25m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ExcelContentType, response.Content.Headers.ContentType?.MediaType);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Assert.Equal("Mobile_Bill_Report_2026_08.xlsx", fileName);

        await using var content = await response.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheet("Monthly Bill Report");
        Assert.Equal("Monthly Mobile Bill Report", sheet.Cell("A1").GetString());
        Assert.Equal("August 2026", sheet.Cell("B2").GetString());
        Assert.Contains("Dialog Telecom", sheet.Cell("B3").GetString());
        Assert.Equal("2026-09-08 05:30", sheet.Cell("B4").GetString());
        Assert.Equal(ExpectedHeaders, sheet.Range("A5:P5").Cells().Select(cell => cell.GetString()));

        var table = sheet.Table("MonthlyBillReportTable");
        Assert.Equal(2, table.DataRange!.RowCount());
        Assert.True(table.ShowAutoFilter);
        Assert.Equal("761499198", table.DataRange.Cell(1, 2).GetString());
        Assert.Equal(120.25m, table.DataRange.Cell(1, 12).GetValue<decimal>());
        Assert.Equal(-999.99m, table.DataRange.Cell(1, 13).GetValue<decimal>());
        Assert.Equal(0m, table.DataRange.Cell(1, 14).GetValue<decimal>());
        Assert.Equal("By User", table.DataRange.Cell(1, 15).GetString());
        Assert.Equal("By Company", table.DataRange.Cell(2, 15).GetString());
        Assert.DoesNotContain(table.DataRange.Rows(), row => row.Cell(2).GetString() == "768791861");

        var totalsRow = table.RangeAddress.LastAddress.RowNumber + 1;
        Assert.Equal("Totals", sheet.Cell(totalsRow, 1).GetString());
        Assert.Equal(200.25m, sheet.Cell(totalsRow, 12).GetValue<decimal>());
        Assert.Equal("Provider / Batch Total", sheet.Cell(totalsRow + 2, 1).GetString());
        Assert.Equal(205.25m, sheet.Cell(totalsRow + 2, 12).GetValue<decimal>());
        Assert.Equal("Less: Excluded Records", sheet.Cell(totalsRow + 3, 1).GetString());
        Assert.Equal(5m, sheet.Cell(totalsRow + 3, 12).GetValue<decimal>());
        Assert.Equal("Report Actual Bill Total", sheet.Cell(totalsRow + 4, 1).GetString());
        Assert.Equal(200.25m, sheet.Cell(totalsRow + 4, 12).GetValue<decimal>());
        Assert.Equal("#,##0.00", sheet.Cell(totalsRow, 12).Style.NumberFormat.Format);
    }

    [Fact]
    public async Task Unassessed_included_row_blocks_export()
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, [ValidRow(assessed: false)], 100m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Assessed_row_with_null_responsibility_blocks_export()
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, [ValidRow(responsibility: null)], 100m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Missing_required_transaction_snapshot_blocks_export()
    {
        using var fixture = new ReportApiFixture();
        var row = ValidRow() with { EmployeeName = " " };
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, [row], 100m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Unresolved_blocking_exception_blocks_export()
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, [ValidRow()], 100m, addBlockingException: true);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reconciliation_failure_blocks_export()
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed,
            [ValidRow(actualBill: 100m), ValidRow("768791861", null, 5m, status: MonthlyBillStatus.Excluded, assessed: false)],
            106m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Sample_report_reference_exports_258_rows_and_exact_actual_bill_total()
    {
        using var fixture = new ReportApiFixture();
        var rows = ReadSampleReportRows();
        Assert.Equal(258, rows.Count);
        Assert.Equal(390096.74m, rows.Sum(row => row.ActualBill));
        var batchId = await fixture.SeedAsync(BillBatchStatus.Locked, rows, 390096.74m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var firstOpen = new XLWorkbook(new MemoryStream(bytes));
        Assert.True(firstOpen.TryGetWorksheet("Monthly Bill Report", out var report));
        var table = report!.Table("MonthlyBillReportTable");
        Assert.Equal(258, table.DataRange!.RowCount());
        var totalsRow = table.RangeAddress.LastAddress.RowNumber + 1;
        Assert.Equal(390096.74m, report.Cell(totalsRow, 12).GetValue<decimal>());

        using var reopened = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(390096.74m, reopened.Worksheet("Monthly Bill Report").Cell(totalsRow, 12).GetValue<decimal>());
    }

    private static RowSeed ValidRow(
        string mobile = "761499100",
        Responsibility? responsibility = Responsibility.ByUser,
        decimal actualBill = 100m,
        decimal creditLimit = 50m,
        decimal monthlyRental = 25m,
        decimal variance = -25m,
        decimal finalDeduction = 25m,
        string? remark = null,
        MonthlyBillStatus status = MonthlyBillStatus.Approved,
        bool assessed = true) =>
        new(mobile, "EPF-1", "Employee One", "Manager", "Engineer", "Factory A", "IT", "Sam", creditLimit,
            monthlyRental, actualBill, variance, finalDeduction, responsibility, remark, status, assessed ? GeneratedAt : null);

    private static IReadOnlyList<RowSeed> ReadSampleReportRows()
    {
        using var workbook = new XLWorkbook(FindSample("sample-report.xlsx"));
        var sheet = workbook.Worksheet("Master- August1");
        var rows = new List<RowSeed>();
        for (var rowNumber = 5; rowNumber <= 262; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            var responsibility = row.Cell(15).GetString().Contains("Company", StringComparison.OrdinalIgnoreCase)
                ? Responsibility.ByCompany
                : Responsibility.ByUser;
            rows.Add(new RowSeed(
                row.Cell(2).GetFormattedString(),
                string.IsNullOrWhiteSpace(row.Cell(3).GetFormattedString()) ? $"TEST-LEGACY-{rowNumber}" : row.Cell(3).GetFormattedString(),
                row.Cell(4).GetString(),
                row.Cell(5).GetString(), row.Cell(6).GetString(), row.Cell(7).GetString(), row.Cell(8).GetString(),
                row.Cell(9).GetString(), row.Cell(10).GetValue<decimal>(), row.Cell(11).GetValue<decimal>(),
                row.Cell(12).GetValue<decimal>(), row.Cell(13).GetValue<decimal>(), 0m, responsibility, null,
                MonthlyBillStatus.Approved, GeneratedAt));
        }
        return rows;
    }

    private static string FindSample(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "samples", fileName);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"Could not locate docs/samples/{fileName}.");
    }

    private sealed record RowSeed(
        string MobileNumber, string Epf, string EmployeeName, string Category, string Designation, string Factory,
        string Department, string CallingName, decimal CreditLimit, decimal MonthlyRental, decimal ActualBill,
        decimal Variance, decimal FinalDeduction, Responsibility? Responsibility, string? Remark,
        MonthlyBillStatus Status, DateTimeOffset? AssessedAt);

    private sealed class ReportApiFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public ReportApiFixture()
        {
            var databaseName = $"billing-report-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FixedClock(GeneratedAt));
            }));
            Client = factory.CreateClient();
        }

        public async Task<Guid> SeedAsync(BillBatchStatus status, IReadOnlyCollection<RowSeed> rows, decimal? calculatedGrandTotal, bool addBlockingException = false)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var provider = new TelecomProvider { Code = "DIALOG", Name = "Dialog Telecom" };
            var batch = new BillBatch
            {
                ProviderId = provider.Id, Provider = provider, CorporateCode = "PR48799679", BillingYear = 2026,
                BillingMonth = 8, Status = status, CalculatedGrandTotal = calculatedGrandTotal,
                ValidationLevel = ValidationLevel.StructuralOnly
            };
            db.AddRange(provider, batch);
            foreach (var row in rows)
            {
                var line = new BillLine
                {
                    BillBatchId = batch.Id, MobileNumber = row.MobileNumber, RawText = row.MobileNumber,
                    PageNumber = 1, ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = row.ActualBill
                };
                db.Add(line);
                db.Add(new MonthlyBill
                {
                    EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = line.Id,
                    CreditLimit = row.CreditLimit, MonthlyRental = row.MonthlyRental, ActualBill = row.ActualBill,
                    Variance = row.Variance, CalculatedExcess = 999.88m, FinalDeduction = row.FinalDeduction,
                    Responsibility = row.Responsibility, AssessedAt = row.AssessedAt, AssessedBy = row.AssessedAt is null ? null : "dev-user",
                    Remark = row.Remark, Status = row.Status, EmployeeEpfSnapshot = row.Epf,
                    EmployeeNameSnapshot = row.EmployeeName, CallingNameSnapshot = row.CallingName,
                    CategoryCodeSnapshot = "CAT", CategoryNameSnapshot = row.Category,
                    DesignationCodeSnapshot = "DES", DesignationNameSnapshot = row.Designation,
                    FactoryCodeSnapshot = "FAC", FactoryNameSnapshot = row.Factory,
                    DepartmentCodeSnapshot = "DEP", DepartmentNameSnapshot = row.Department,
                    MobileNumberSnapshot = row.MobileNumber,
                    EntitlementEffectiveFromSnapshot = new DateOnly(2026, 1, 1)
                });
            }
            if (addBlockingException)
            {
                db.Add(new BillException
                {
                    BillBatchId = batch.Id, ExceptionType = BillExceptionType.MOBILE_NOT_FOUND,
                    Severity = BillExceptionSeverity.Blocking, Status = BillExceptionStatus.Open,
                    Description = "Unresolved matching exception"
                });
            }
            await db.SaveChangesAsync();
            return batch.Id;
        }

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
