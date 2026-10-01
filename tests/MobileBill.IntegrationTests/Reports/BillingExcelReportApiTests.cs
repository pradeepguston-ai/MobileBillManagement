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
        // By Company rows list the excess the company absorbs (999.88 is the seeded CalculatedExcess).
        Assert.Equal(999.88m, table.DataRange.Cell(2, 14).GetValue<decimal>());
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
        Assert.Equal(999.88m, sheet.Cell(totalsRow, 14).GetValue<decimal>());
        Assert.Equal("Deducted from Employees (By User)", sheet.Cell(totalsRow + 6, 1).GetString());
        Assert.Equal(0m, sheet.Cell(totalsRow + 6, 14).GetValue<decimal>());
        Assert.Equal("Borne by Company (By Company)", sheet.Cell(totalsRow + 7, 1).GetString());
        Assert.Equal(999.88m, sheet.Cell(totalsRow + 7, 14).GetValue<decimal>());
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
    public async Task Unresolved_exception_lines_are_exported_with_employee_columns_blank()
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, [ValidRow(actualBill: 100m)], 130m, [("742253933", 30m)]);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheet("Monthly Bill Report");
        var table = sheet.Table("MonthlyBillReportTable");
        Assert.Equal(2, table.DataRange!.RowCount());
        var exceptionRow = Assert.Single(table.DataRange.Rows(), row => row.Cell(2).GetString() == "742253933");
        Assert.Equal(30m, exceptionRow.Cell(12).GetValue<decimal>());
        foreach (var column in new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16 })
            Assert.True(exceptionRow.Cell(column).IsEmpty(), $"Column {column} should be blank for an exception line.");
        var totalsRow = table.RangeAddress.LastAddress.RowNumber + 1;
        Assert.Equal(130m, sheet.Cell(totalsRow, 12).GetValue<decimal>());
    }

    [Theory]
    [InlineData(BillBatchStatus.Draft)]
    [InlineData(BillBatchStatus.Validated)]
    [InlineData(BillBatchStatus.FinanceApproval)]
    public async Task Non_completed_batch_cannot_be_exported_as_pdf(BillBatchStatus status)
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(status, [ValidRow()], 100m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/pdf");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Pdf_export_has_the_same_data_as_the_excel_report_and_the_system_generated_notice()
    {
        using var fixture = new ReportApiFixture();
        var rows = new[]
        {
            ValidRow("761499198", Responsibility.ByUser, 120.25m, 100m, 20m, -999.99m, 0m, "Approved waiver"),
            ValidRow("761499199", Responsibility.ByCompany, 80m, 40m, 10m, 77.77m, 0m, "Company charge"),
            ValidRow("768791861", null, 5m, status: MonthlyBillStatus.Excluded, assessed: false)
        };
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, rows, 235.25m, [("742253933", 30m)]);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Assert.Equal("Mobile_Bill_Report_2026_08.pdf", fileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var text = string.Join(" ", pdf.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text));
        Assert.Contains("Monthly Mobile Bill Report", text);
        Assert.Contains("August 2026", text);
        Assert.Contains("2026-09-08 05:30", text);
        Assert.Contains("761499198", text);
        Assert.Contains("761499199", text);
        Assert.Contains("742253933", text);          // exception line, employee columns blank
        Assert.DoesNotContain("768791861", text);    // excluded rows stay out of the report
        Assert.Contains("120.25", text);
        Assert.Contains("999.88", text);             // By Company row shows the excess the company absorbs
        Assert.Contains("By Company", text);
        Assert.Contains("Borne by Company (By Company)", text);
        Assert.Contains("Less: Excluded Records", text);
        Assert.Contains("This is a system generated report. No signature required.", text);
    }

    [Fact]
    public async Task Excel_report_also_shows_the_system_generated_notice()
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, [ValidRow(actualBill: 100m)], 100m);

        var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");

        await using var content = await response.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheet("Monthly Bill Report");
        Assert.Equal("This is a system generated report. No signature required.", sheet.Cell("D4").GetString());
        var lastUsedRow = sheet.LastRowUsed()!.RowNumber();
        Assert.Equal("This is a system generated report. No signature required.", sheet.Cell(lastUsedRow, 1).GetString());
    }

    [Fact]
    public async Task Factory_report_contains_only_that_factorys_bills_in_excel_and_pdf()
    {
        using var fixture = new ReportApiFixture();
        var rows = new[]
        {
            ValidRow("761499198", actualBill: 100m),
            ValidRow("761499199", actualBill: 80m) with { Factory = "Factory B" },
        };
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, rows, 210m, [("742253933", 30m)]);

        var excel = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel?factoryCode=FB");

        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal("Mobile_Bill_Report_2026_08_FB.xlsx", excel.Content.Headers.ContentDisposition?.FileNameStar ?? excel.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        await using (var content = await excel.Content.ReadAsStreamAsync())
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheet("Monthly Bill Report");
            Assert.Equal("Factory B (FB)", sheet.Cell("E2").GetString());
            var table = sheet.Table("MonthlyBillReportTable");
            var only = Assert.Single(table.DataRange!.Rows());
            Assert.Equal("761499199", only.Cell(2).GetString());
            Assert.Equal(80m, sheet.Cell(table.RangeAddress.LastAddress.RowNumber + 1, 12).GetValue<decimal>());
        }

        var pdf = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/pdf?factoryCode=FB");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        using var document = UglyToad.PdfPig.PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync());
        var text = string.Join(" ", document.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text));
        Assert.Contains("761499199", text);
        Assert.DoesNotContain("761499198", text);
        Assert.DoesNotContain("742253933", text);    // exception lines belong to no factory
        Assert.Contains("(FB)", text);
    }

    [Fact]
    public async Task Full_report_says_all_factories_and_unknown_factory_is_not_found()
    {
        using var fixture = new ReportApiFixture();
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, [ValidRow(actualBill: 100m)], 100m);

        var full = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel");
        await using (var content = await full.Content.ReadAsStreamAsync())
        {
            using var workbook = new XLWorkbook(content);
            Assert.Equal("All factories", workbook.Worksheet("Monthly Bill Report").Cell("E2").GetString());
        }

        var unknown = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/pdf?factoryCode=NOPE");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Report_can_be_filtered_by_category_and_by_factory_and_category_together()
    {
        using var fixture = new ReportApiFixture();
        var rows = new[]
        {
            ValidRow("761499101", actualBill: 10m),                                                   // Factory A, Manager
            ValidRow("761499102", actualBill: 20m) with { Category = "Staff" },                       // Factory A, Staff
            ValidRow("761499103", actualBill: 30m) with { Factory = "Factory B", Category = "Staff" } // Factory B, Staff
        };
        var batchId = await fixture.SeedAsync(BillBatchStatus.Completed, rows, 60m);

        async Task<(string FileName, string[] Mobiles, decimal Total, string Category)> Excel(string query)
        {
            var response = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel?{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var name = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition!.FileName!.Trim('"');
            await using var content = await response.Content.ReadAsStreamAsync();
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheet("Monthly Bill Report");
            var table = sheet.Table("MonthlyBillReportTable");
            var mobiles = table.DataRange!.Rows().Select(row => row.Cell(2).GetString()).Where(value => value.Length > 0).ToArray();
            return (name, mobiles, sheet.Cell(table.RangeAddress.LastAddress.RowNumber + 1, 12).GetValue<decimal>(), sheet.Cell("H2").GetString());
        }

        var staff = await Excel("categoryCode=STF");
        Assert.Equal(["761499102", "761499103"], staff.Mobiles);
        Assert.Equal(50m, staff.Total);
        Assert.Equal("Staff (STF)", staff.Category);
        Assert.Equal("Mobile_Bill_Report_2026_08_STF.xlsx", staff.FileName);

        var both = await Excel("factoryCode=FB&categoryCode=STF");
        Assert.Equal(["761499103"], both.Mobiles);
        Assert.Equal(30m, both.Total);
        Assert.Equal("Mobile_Bill_Report_2026_08_FB_STF.xlsx", both.FileName);

        var pdf = await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/pdf?factoryCode=FAC&categoryCode=STF");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        using var document = UglyToad.PdfPig.PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync());
        var text = string.Join(" ", document.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text));
        Assert.Contains("761499102", text);
        Assert.DoesNotContain("761499101", text);
        Assert.DoesNotContain("761499103", text);
        Assert.Contains("(STF)", text);

        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/pdf?categoryCode=NOPE")).StatusCode);

        // Several categories at once: every bill in any of them.
        var many = await Excel("categoryCode=CAT&categoryCode=STF");
        Assert.Equal(["761499101", "761499102", "761499103"], many.Mobiles);
        Assert.Equal(60m, many.Total);
        Assert.Equal("Manager (CAT), Staff (STF)", many.Category);
        Assert.Equal("Mobile_Bill_Report_2026_08_CAT-STF.xlsx", many.FileName);

        var manyWithFactory = await Excel("factoryCode=FAC&categoryCode=CAT&categoryCode=STF");
        Assert.Equal(["761499101", "761499102"], manyWithFactory.Mobiles);

        // Several factories at once, combined with several categories.
        var manyFactories = await Excel("factoryCode=FAC&factoryCode=FB&categoryCode=STF");
        Assert.Equal(["761499102", "761499103"], manyFactories.Mobiles);
        Assert.Equal(50m, manyFactories.Total);
        Assert.Equal("Mobile_Bill_Report_2026_08_FAC-FB_STF.xlsx", manyFactories.FileName);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/pdf?factoryCode=FAC&factoryCode=NOPE")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/reports/billing/{batchId}/excel?categoryCode=STF&categoryCode=NOPE")).StatusCode);
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

        public async Task<Guid> SeedAsync(BillBatchStatus status, IReadOnlyCollection<RowSeed> rows, decimal? calculatedGrandTotal, IReadOnlyCollection<(string Mobile, decimal Amount)>? unmatchedLines = null)
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
            db.AddRange(provider, batch, new Factory { Code = "FAC", Name = "Factory A" }, new Factory { Code = "FB", Name = "Factory B" }, new EmployeeCategory { Code = "CAT", Name = "Manager" }, new EmployeeCategory { Code = "STF", Name = "Staff" });
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
                    CategoryCodeSnapshot = row.Category == "Staff" ? "STF" : "CAT", CategoryNameSnapshot = row.Category,
                    DesignationCodeSnapshot = "DES", DesignationNameSnapshot = row.Designation,
                    FactoryCodeSnapshot = row.Factory == "Factory B" ? "FB" : "FAC", FactoryNameSnapshot = row.Factory,
                    DepartmentCodeSnapshot = "DEP", DepartmentNameSnapshot = row.Department,
                    MobileNumberSnapshot = row.MobileNumber,
                    EntitlementEffectiveFromSnapshot = new DateOnly(2026, 1, 1)
                });
            }
            foreach (var (mobile, amount) in unmatchedLines ?? [])
            {
                var line = new BillLine
                {
                    BillBatchId = batch.Id, MobileNumber = mobile, RawText = mobile,
                    PageNumber = 1, ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = amount
                };
                db.Add(line);
                db.Add(new BillException
                {
                    BillBatchId = batch.Id, BillLineId = line.Id, ExceptionType = BillExceptionType.MOBILE_NOT_FOUND,
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
