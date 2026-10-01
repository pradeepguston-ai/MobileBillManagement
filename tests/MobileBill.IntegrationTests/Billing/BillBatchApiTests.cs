using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Enums;
using MobileBill.IntegrationTests;

namespace MobileBill.IntegrationTests.Billing;

public sealed class BillBatchApiTests
{
    [Fact]
    public async Task Create_endpoint_uses_the_bill_batch_service_and_does_not_accept_a_writable_upload_identity()
    {
        var id = Guid.NewGuid();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBillBatchService>();
            services.AddSingleton<IBillBatchService>(new FakeBillBatchService(id));
        }));
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);

        var response = await client.PostAsJsonAsync("/api/bill-batches", new { providerId = Guid.NewGuid(), corporateCode = "CORP", billingYear = 2026, billingMonth = 9, uploadedBy = "spoofed" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var batch = await response.Content.ReadFromJsonAsync<BillBatchDto>();
        Assert.NotNull(batch);
        Assert.Equal("Development User", batch.UploadedBy);
        Assert.Equal(id, batch.Id);
    }

    private sealed class FakeBillBatchService(Guid id) : IBillBatchService
    {
        public Task<PagedResult<BillBatchListItemDto>> ListAsync(BillBatchListRequest request, CancellationToken token) => Task.FromResult(new PagedResult<BillBatchListItemDto>([], 1, 20, 0));
        private static BillBatchDto Batch(Guid id) => new(id, Guid.Empty, "CORP", 2026, 9, BillBatchStatus.Draft, null, null, "Development User", DateTimeOffset.UnixEpoch, null, null, null, GrandTotalSource.None, ValidationLevel.None, null);
        public Task<BillBatchDto> CreateAsync(CreateBillBatchRequest request, CancellationToken token) => Task.FromResult(Batch(id));
        public Task<BillBatchDto> UploadAsync(Guid id, Stream content, string fileName, string? contentType, long length, CancellationToken token) => Task.FromResult(Batch(id));
        public Task<BillBatchDto> ParseAsync(Guid id, CancellationToken token) => Task.FromResult(Batch(id));
        public Task<BillBatchDto> GetAsync(Guid id, CancellationToken token) => Task.FromResult(Batch(id));
        public Task<PagedResult<BillLineDto>> GetLinesAsync(Guid id, PagedRequest request, CancellationToken token) => Task.FromResult(new PagedResult<BillLineDto>([], 1, 20, 0));
        public Task<BillBatchDto> ValidateAsync(Guid id, CancellationToken token) => Task.FromResult(Batch(id));
    }
}
