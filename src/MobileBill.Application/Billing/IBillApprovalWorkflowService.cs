namespace MobileBill.Application.Billing;

public interface IBillApprovalWorkflowService
{
    Task<BillWorkflowCapabilitiesDto> GetCapabilitiesAsync(Guid batchId, CancellationToken cancellationToken);
    Task<BillWorkflowResult> SubmitAsync(Guid batchId, WorkflowCommentRequest request, CancellationToken cancellationToken);
    Task<BillWorkflowResult> DecideAsync(Guid batchId, WorkflowDecisionRequest request, CancellationToken cancellationToken);
    Task<BillWorkflowResult> LockAsync(Guid batchId, CancellationToken cancellationToken);
}
