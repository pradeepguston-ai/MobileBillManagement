using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Insights;

namespace MobileBill.Api.Controllers;

[ApiController, Route("api/insights"), Authorize]
public sealed class InsightsController(IBillingInsightsService insights) : ControllerBase
{
    // batchId: the batch to analyse (latest matched batch when omitted). months: how many approved batches the trend covers.
    [HttpGet]
    public Task<BillingInsightsDto> Get([FromQuery] Guid? batchId, [FromQuery] int months = 6, CancellationToken token = default)
        => insights.GetAsync(batchId, months, token);
}
