using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Billing;

namespace MobileBill.Api.Controllers;

[ApiController]
[Route("api/bill-batches/{batchId:guid}")]
[Authorize]
public sealed class BillBatchWorkflowController(IBillApprovalWorkflowService workflowService) : ControllerBase
{
    [HttpGet("workflow-capabilities")]
    public Task<BillWorkflowCapabilitiesDto> Capabilities(Guid batchId, CancellationToken cancellationToken) => workflowService.GetCapabilitiesAsync(batchId, cancellationToken);

    [HttpPost("workflow/submit")]
    public Task<BillWorkflowResult> Submit(Guid batchId, [FromBody] WorkflowCommentRequest request, CancellationToken cancellationToken) => workflowService.SubmitAsync(batchId, request, cancellationToken);

    [HttpPost("workflow/decision")]
    public Task<BillWorkflowResult> Decide(Guid batchId, [FromBody] WorkflowDecisionRequest request, CancellationToken cancellationToken) => workflowService.DecideAsync(batchId, request, cancellationToken);

    [HttpPost("lock")]
    public Task<BillWorkflowResult> Lock(Guid batchId, CancellationToken cancellationToken) => workflowService.LockAsync(batchId, cancellationToken);
}
