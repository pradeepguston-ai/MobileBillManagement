using MobileBill.Domain.Enums;

namespace MobileBill.Application.Billing;

public interface IBillReviewAuthorizationService
{
    Task<bool> CanResolveExceptionsAsync(CancellationToken cancellationToken);
    Task<bool> AuthorizeAsync(BillWorkflowAction action, CancellationToken cancellationToken) => Task.FromResult(false);
}
