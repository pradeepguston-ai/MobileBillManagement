using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Billing;

public sealed class BillExceptionReadApiTests
{
    [Fact]
    public async Task Generic_exception_query_remains_batch_scoped()
    {
        using var fixture = new ExceptionApiFixture();
        var batch = await fixture.SeedBatchAsync();
        var otherBatch = await fixture.SeedBatchAsync();
        await fixture.SeedExceptionAsync(batch, BillExceptionType.MOBILE_NOT_FOUND, BillExceptionStatus.Open);
        await fixture.SeedExceptionAsync(otherBatch, BillExceptionType.ZERO_BILL, BillExceptionStatus.Open);

        var result = await fixture.Client.GetFromJsonAsync<PagedResult<BillExceptionDto>>($"/api/bill-batches/{batch}/exceptions?pageNumber=1&pageSize=20");

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(batch, item.BillBatchId));
    }

    [Fact]
    public async Task Exception_summary_returns_whole_batch_resolution_counts()
    {
        using var fixture = new ExceptionApiFixture();
        var batch = await fixture.SeedBatchAsync();
        await fixture.SeedExceptionAsync(batch, BillExceptionType.MOBILE_NOT_FOUND, BillExceptionStatus.Open);
        await fixture.SeedExceptionAsync(batch, BillExceptionType.PARSER_WARNING, BillExceptionStatus.InReview);
        await fixture.SeedExceptionAsync(batch, BillExceptionType.ZERO_BILL, BillExceptionStatus.Resolved);
        await fixture.SeedExceptionAsync(batch, BillExceptionType.EMPLOYEE_NOT_ACTIVE, BillExceptionStatus.Waived);

        var result = await fixture.Client.GetFromJsonAsync<ExceptionSummaryResponse>($"/api/bill-batches/{batch}/exceptions/summary");

        Assert.NotNull(result);
        Assert.Equal(batch, result.BillBatchId);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.UnresolvedCount);
        Assert.Equal(2, result.ResolvedCount);
    }

    [Theory]
    [InlineData("exceptionType=ZERO_BILL", 2)]
    [InlineData("resolution=Unresolved", 2)]
    [InlineData("resolution=Resolved", 2)]
    [InlineData("exceptionType=ZERO_BILL&resolution=Resolved", 1)]
    public async Task Exception_rows_support_type_resolution_and_combined_filters(string query, int expectedCount)
    {
        using var fixture = new ExceptionApiFixture();
        var batch = await fixture.SeedBatchAsync();
        await fixture.SeedExceptionAsync(batch, BillExceptionType.ZERO_BILL, BillExceptionStatus.Open);
        await fixture.SeedExceptionAsync(batch, BillExceptionType.ZERO_BILL, BillExceptionStatus.Resolved);
        await fixture.SeedExceptionAsync(batch, BillExceptionType.PARSER_WARNING, BillExceptionStatus.InReview);
        await fixture.SeedExceptionAsync(batch, BillExceptionType.MOBILE_NOT_FOUND, BillExceptionStatus.Waived);

        var result = await fixture.Client.GetFromJsonAsync<PagedResult<BillExceptionDto>>($"/api/bill-batches/{batch}/exceptions?pageNumber=1&pageSize=20&{query}");

        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.TotalCount);
    }

    [Fact]
    public async Task Missing_batch_summary_returns_not_found()
    {
        using var fixture = new ExceptionApiFixture();

        var response = await fixture.Client.GetAsync($"/api/bill-batches/{Guid.NewGuid()}/exceptions/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class ExceptionApiFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public ExceptionApiFixture()
        {
            var databaseName = $"exceptions-{Guid.NewGuid():N}";
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

        public async Task<Guid> SeedBatchAsync()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var provider = new TelecomProvider { Code = $"P-{Guid.NewGuid():N}"[..12], Name = "Telecom" };
            var batch = new BillBatch { ProviderId = provider.Id, Provider = provider, CorporateCode = "CORP", BillingYear = 2026, BillingMonth = 8 };
            db.AddRange(provider, batch);
            await db.SaveChangesAsync();
            return batch.Id;
        }

        public async Task SeedExceptionAsync(Guid batchId, BillExceptionType type, BillExceptionStatus status)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var line = new BillLine { BillBatchId = batchId, MobileNumber = $"7{Random.Shared.Next(10000000, 99999999)}", PageNumber = 1, RawText = "row", ExtractionStatus = BillLineExtractionStatus.Extracted };
            db.AddRange(line, new BillException { BillBatchId = batchId, BillLineId = line.Id, BillLine = line, ExceptionType = type, Severity = BillExceptionSeverity.Warning, Status = status, Description = type.ToString() });
            await db.SaveChangesAsync();
        }

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
        }
    }

    private sealed record ExceptionSummaryResponse(Guid BillBatchId, int TotalCount, int UnresolvedCount, int ResolvedCount);
}
