using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Billing;

namespace MobileBill.Api.Controllers;

[ApiController, Authorize]
[Route("api/monthly-bills")]
public sealed class MonthlyBillsController(IBillAssessmentService service) : ControllerBase
{
    [HttpPut("{id:guid}/assessment"), Authorize]
    public Task<MonthlyBillAssessmentDto> Assess(Guid id, AssessMonthlyBillRequest request, CancellationToken cancellationToken) =>
        service.AssessAsync(id, request, cancellationToken);

    [HttpPost("bulk-assessment"), Authorize]
    public Task<BulkAssessmentResultDto> BulkAssess(BulkAssessMonthlyBillsRequest request, CancellationToken cancellationToken) =>
        service.BulkAssessAsync(request, cancellationToken);
}
