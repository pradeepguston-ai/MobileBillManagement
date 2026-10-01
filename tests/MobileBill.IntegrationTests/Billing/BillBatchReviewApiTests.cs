using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.IntegrationTests.Billing;

public sealed class BillBatchReviewApiTests
{
    [Fact]
    public async Task Summary_and_rows_endpoints_are_batch_scoped_and_use_the_review_query_service()
    {
        var batchId = Guid.NewGuid();
        var service = new FakeReviewQueryService(batchId);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBillBatchReviewQueryService>();
            services.AddSingleton<IBillBatchReviewQueryService>(service);
        }));
        using var client = factory.CreateClient();

        var summaryResponse = await client.GetAsync($"/api/bill-batches/{batchId}/review");
        var rowsResponse = await client.GetAsync($"/api/bill-batches/{batchId}/review/rows?pageNumber=2&pageSize=20&responsibility=Unassessed");

        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rowsResponse.StatusCode);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<BillBatchReviewSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(batchId, summary.BatchId);
        Assert.NotNull(service.RowsRequest);
        Assert.Equal(ReviewResponsibilityFilter.Unassessed, service.RowsRequest!.Responsibility);
    }

    private sealed class FakeReviewQueryService(Guid expectedBatchId) : IBillBatchReviewQueryService
    {
        public BillReviewRowsRequest? RowsRequest { get; private set; }
        public Task<BillBatchReviewSummaryDto> GetSummaryAsync(Guid batchId, CancellationToken cancellationToken)
        {
            Assert.Equal(expectedBatchId, batchId);
            return Task.FromResult(new BillBatchReviewSummaryDto(batchId, 2026, 8, "Telecom", "C", BillBatchStatus.Validated, ValidationLevel.StructuralOnly, 1, 100m, 0m, 0m, 0m, 0, 0, 0, 1));
        }
        public Task<PagedResult<BillReviewRowDto>> GetRowsAsync(Guid batchId, BillReviewRowsRequest request, CancellationToken cancellationToken)
        {
            Assert.Equal(expectedBatchId, batchId); RowsRequest = request;
            return Task.FromResult(new PagedResult<BillReviewRowDto>([], request.NormalizedPageNumber, request.NormalizedPageSize, 0));
        }
        public Task<BillReviewDetailDto> GetDetailAsync(Guid batchId, Guid monthlyBillId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
