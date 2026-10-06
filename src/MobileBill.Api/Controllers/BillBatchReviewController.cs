using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;

namespace MobileBill.Api.Controllers;

[ApiController, Authorize]
[Route("api/bill-batches/{batchId:guid}/review")]
public sealed class BillBatchReviewController(IBillBatchReviewQueryService queryService, IBillTrendService trendService) : ControllerBase
{
    [HttpGet] public Task<BillBatchReviewSummaryDto> Summary(Guid batchId, CancellationToken cancellationToken) => queryService.GetSummaryAsync(batchId, cancellationToken);
    [HttpGet("rows")] public Task<PagedResult<BillReviewRowDto>> Rows(Guid batchId, [FromQuery] BillReviewRowsRequest request, CancellationToken cancellationToken) => queryService.GetRowsAsync(batchId, request, cancellationToken);
    [HttpGet("rows/{monthlyBillId:guid}")] public Task<BillReviewDetailDto> Detail(Guid batchId, Guid monthlyBillId, CancellationToken cancellationToken) => queryService.GetDetailAsync(batchId, monthlyBillId, cancellationToken);
    [HttpGet("rows/{monthlyBillId:guid}/trend")] public Task<BillTrendDto> Trend(Guid batchId, Guid monthlyBillId, [FromQuery] BillTrendRequest request, CancellationToken cancellationToken) => trendService.GetAsync(batchId, monthlyBillId, request, cancellationToken);
}
