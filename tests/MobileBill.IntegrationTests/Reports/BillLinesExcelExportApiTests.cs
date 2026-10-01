using System.Net;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Reports;

public sealed class BillLinesExcelExportApiTests
{
    [Fact]
    public async Task Exports_every_extracted_line_with_the_screen_columns_and_totals()
    {
        using var fixture = new LinesFixture();
        var batchId = await fixture.SeedAsync();

        var response = await fixture.Client.GetAsync($"/api/bill-batches/{batchId}/lines/excel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Extracted_Bill_Lines_2026_08.xlsx", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        await using var content = await response.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheet("Extracted Bill Lines");
        Assert.Equal("August 2026", sheet.Cell("B2").GetString());
        Assert.Equal("All lines", sheet.Cell("E4").GetString());

        var table = sheet.Table("ExtractedBillLinesTable");
        var headers = table.HeadersRow().Cells().Select(cell => cell.GetString()).ToList();
        Assert.Equal(["Serial", "Mobile Account", "Previous Due", "Payments", "Total Usage", "IDD", "Roaming"], headers.Take(7));
        Assert.Equal(["Total Due", "Extraction Status", "Page Number", "Extraction Error"], headers.TakeLast(4));
        Assert.Equal(3, table.DataRange!.RowCount());

        var roamingColumn = headers.IndexOf("Roaming") + 1;
        var totalDueColumn = headers.IndexOf("Total Due") + 1;
        var first = table.DataRange.Row(1);
        Assert.Equal("761499101", first.Cell(2).GetString());
        Assert.Equal(12.5m, first.Cell(roamingColumn).GetValue<decimal>());
        var failed = table.DataRange.Rows().Single(row => row.Cell(2).GetString() == "761499103");
        Assert.Equal("ValidationFailed", failed.Cell(headers.IndexOf("Extraction Status") + 1).GetString());
        Assert.Equal("Could not read amount", failed.Cell(headers.IndexOf("Extraction Error") + 1).GetString());

        var totalsRow = table.RangeAddress.LastAddress.RowNumber + 1;
        Assert.Equal("Totals", sheet.Cell(totalsRow, 1).GetString());
        Assert.Equal(300m, sheet.Cell(totalsRow, totalDueColumn).GetValue<decimal>());
    }

    [Fact]
    public async Task Search_limits_the_export_to_matching_mobile_numbers()
    {
        using var fixture = new LinesFixture();
        var batchId = await fixture.SeedAsync();

        var response = await fixture.Client.GetAsync($"/api/bill-batches/{batchId}/lines/excel?search=102");

        await using var content = await response.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheet("Extracted Bill Lines");
        Assert.Equal("102", sheet.Cell("E4").GetString());
        Assert.Equal("761499102", Assert.Single(sheet.Table("ExtractedBillLinesTable").DataRange!.Rows()).Cell(2).GetString());
    }

    [Fact]
    public async Task Unknown_batch_is_not_found()
    {
        using var fixture = new LinesFixture();

        var response = await fixture.Client.GetAsync($"/api/bill-batches/{Guid.NewGuid()}/lines/excel");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class LinesFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public LinesFixture()
        {
            var databaseName = $"lines-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }));
            Client = factory.CreateClient();
        }

        public async Task<Guid> SeedAsync()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var provider = new TelecomProvider { Code = "DIALOG", Name = "Dialog Telecom" };
            var batch = new BillBatch { ProviderId = provider.Id, Provider = provider, CorporateCode = "PR48799679", BillingYear = 2026, BillingMonth = 8, Status = BillBatchStatus.Parsed };
            db.AddRange(provider, batch,
                new BillLine { BillBatchId = batch.Id, MobileNumber = "761499101", PageNumber = 1, RawText = "a", ExtractionStatus = BillLineExtractionStatus.Extracted, Roaming = 12.5m, TotalDueAmount = 100m },
                new BillLine { BillBatchId = batch.Id, MobileNumber = "761499102", PageNumber = 2, RawText = "b", ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = 200m },
                new BillLine { BillBatchId = batch.Id, MobileNumber = "761499103", PageNumber = 3, RawText = "c", ExtractionStatus = BillLineExtractionStatus.ValidationFailed, ExtractionError = "Could not read amount" });
            await db.SaveChangesAsync();
            return batch.Id;
        }

        public void Dispose() { Client.Dispose(); factory.Dispose(); }
    }
}
