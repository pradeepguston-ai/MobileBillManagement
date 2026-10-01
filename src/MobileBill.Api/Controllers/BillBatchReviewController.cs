using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;

namespace MobileBill.Api.Controllers;

[ApiController]
[Route("api/bill-batches/{batchId:guid}/review")]
public sealed class BillBatchReviewController(IBillBatchReviewQueryService queryService) : ControllerBase
{
    [HttpGet] public Task<BillBatchReviewSummaryDto> Summary(Guid batchId, CancellationToken cancellationToken) => queryService.GetSummaryAsync(batchId, cancellationToken);
    [HttpGet("rows")] public Task<PagedResult<BillReviewRowDto>> Rows(Guid batchId, [FromQuery] BillReviewRowsRequest request, CancellationToken cancellationToken) => queryService.GetRowsAsync(batchId, request, cancellationToken);
    [HttpGet("rows/{monthlyBillId:guid}")] public Task<BillReviewDetailDto> Detail(Guid batchId, Guid monthlyBillId, CancellationToken cancellationToken) => queryService.GetDetailAsync(batchId, monthlyBillId, cancellationToken);
}
