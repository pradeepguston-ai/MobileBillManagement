using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Billing;

public sealed class BillBatchProcessingReadApiTests
{
    [Fact]
    public async Task Batch_list_is_paged_and_defaults_to_latest_billing_period_first()
    {
        using var fixture = new BillingReadApiFixture();
        await fixture.SeedBatchAsync(2025, 12, "Older");
        var latest = await fixture.SeedBatchAsync(2026, 8, "Latest");
        await fixture.SeedBatchAsync(2026, 7, "Middle");

        var response = await fixture.Client.GetFromJsonAsync<PagedBatchResponse>("/api/bill-batches?page=1&pageSize=2");

        Assert.NotNull(response);
        Assert.Equal(3, response.TotalCount);
        Assert.Equal(2, response.TotalPages);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(latest, response.Items[0].Id);
        Assert.Equal(2026, response.Items[0].BillingYear);
        Assert.Equal(8, response.Items[0].BillingMonth);
    }

    [Fact]
    public async Task Batch_list_supports_whitelisted_sorting_and_unsupported_sort_falls_back_safely()
    {
        using var fixture = new BillingReadApiFixture();
        var dialog = await fixture.SeedBatchAsync(2026, 7, "Dialog", calculatedGrandTotal: 300m);
        var airtel = await fixture.SeedBatchAsync(2026, 8, "Airtel", calculatedGrandTotal: 100m);

        var supported = await fixture.Client.GetFromJsonAsync<PagedBatchResponse>("/api/bill-batches?page=1&pageSize=20&sortBy=calculatedGrandTotal&sortDirection=asc");
        var unsupported = await fixture.Client.GetFromJsonAsync<PagedBatchResponse>("/api/bill-batches?page=1&pageSize=20&sortBy=storedFilePath&sortDirection=asc");

        Assert.NotNull(supported);
        Assert.Equal([airtel, dialog], supported.Items.Select(item => item.Id));
        Assert.NotNull(unsupported);
        Assert.Equal([airtel, dialog], unsupported.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Batch_list_exposes_original_filename_but_never_physical_storage_path()
    {
        using var fixture = new BillingReadApiFixture();
        await fixture.SeedBatchAsync(2026, 8, "Dialog", originalFileName: "August Bill.pdf", storedFilePath: "App_Data/BillUploads/private.pdf");

        var response = await fixture.Client.GetAsync("/api/bill-batches?page=1&pageSize=20");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("August Bill.pdf", json);
        Assert.DoesNotContain("storedFilePath", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("App_Data/BillUploads", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Batch_detail_reconstructs_parser_counts_and_preserves_stored_calculated_total()
    {
        using var fixture = new BillingReadApiFixture();
        var batchId = await fixture.SeedBatchAsync(2026, 8, "Dialog", calculatedGrandTotal: 777.77m,
            lines:
            [
                new("761499100", BillLineExtractionStatus.Extracted, 100m, null),
                new("761499101", BillLineExtractionStatus.Extracted, 200m, null),
                new("761499102", BillLineExtractionStatus.ValidationFailed, 0m, "Malformed candidate")
            ], validationWarning: "Source PDF grand total could not be independently extracted.");

        var response = await fixture.Client.GetAsync($"/api/bill-batches/{batchId}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, json.GetProperty("totalCandidates").GetInt32());
        Assert.Equal(2, json.GetProperty("successfulCount").GetInt32());
        Assert.Equal(1, json.GetProperty("failedCount").GetInt32());
        Assert.Equal(777.77m, json.GetProperty("calculatedGrandTotal").GetDecimal());
        Assert.Contains(json.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()),
            warning => warning?.Contains("independently extracted", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task Extracted_lines_search_is_trimmed_and_applied_server_side_to_mobile_number()
    {
        using var fixture = new BillingReadApiFixture();
        var batchId = await fixture.SeedBatchAsync(2026, 8, "Dialog",
            lines:
            [
                new("761499100", BillLineExtractionStatus.Extracted, 100m, null),
                new("768791861", BillLineExtractionStatus.Extracted, 0m, null),
                new("777000000", BillLineExtractionStatus.ValidationFailed, 0m, "Malformed")
            ]);

        var result = await fixture.Client.GetFromJsonAsync<PagedLineResponse>($"/api/bill-batches/{batchId}/lines?pageNumber=1&pageSize=20&search=%20%2076879%20%20");

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("768791861", result.Items[0].MobileNumber);
    }

    private sealed class BillingReadApiFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public BillingReadApiFixture()
        {
            var databaseName = $"billing-read-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }));
            Client = factory.CreateClient();
            // Read endpoints need a signed-in user; any role may read.
            TestAuth.Authorize(Client, MobileBill.Domain.Enums.UserRole.Cfo);
        }

        public async Task<Guid> SeedBatchAsync(
            int billingYear,
            int billingMonth,
            string providerName,
            decimal? calculatedGrandTotal = null,
            string? originalFileName = null,
            string? storedFilePath = null,
            IReadOnlyList<LineSeed>? lines = null,
            string? validationWarning = null)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var provider = new TelecomProvider { Code = providerName.ToUpperInvariant(), Name = providerName };
            var batch = new BillBatch
            {
                ProviderId = provider.Id,
                CorporateCode = $"CORP-{billingYear}-{billingMonth:D2}",
                BillingYear = billingYear,
                BillingMonth = billingMonth,
                OriginalFileName = originalFileName,
                StoredFilePath = storedFilePath,
                CalculatedGrandTotal = calculatedGrandTotal,
                ValidationWarning = validationWarning,
                Status = BillBatchStatus.Parsed,
                UploadedBy = "Development User",
                UploadedAt = new DateTimeOffset(billingYear, billingMonth, 1, 0, 0, 0, TimeSpan.Zero)
            };
            db.AddRange(provider, batch);
            foreach (var line in lines ?? [])
            {
                db.Add(new BillLine
                {
                    BillBatchId = batch.Id,
                    MobileNumber = line.MobileNumber,
                    PageNumber = 1,
                    RawText = line.MobileNumber,
                    ExtractionStatus = line.Status,
                    ExtractionError = line.Error,
                    TotalDueAmount = line.TotalDueAmount
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

    private sealed record LineSeed(string MobileNumber, BillLineExtractionStatus Status, decimal TotalDueAmount, string? Error);
    private sealed record BatchItem(Guid Id, int BillingYear, int BillingMonth, string Provider, string CorporateCode, decimal? CalculatedGrandTotal, string ValidationLevel, string Status, string? OriginalFileName, string? UploadedBy, DateTimeOffset? UploadedAt);
    private sealed record PagedBatchResponse(IReadOnlyList<BatchItem> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);
    private sealed record LineItem(string MobileNumber);
    private sealed record PagedLineResponse(IReadOnlyList<LineItem> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);
}
