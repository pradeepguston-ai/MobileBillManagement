using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Identity;

public sealed class RoleBasedBillReviewAuthorizationService(ICurrentUserService currentUser) : IBillReviewAuthorizationService
{
    private static readonly Dictionary<BillWorkflowAction, UserRole[]> ActionRoles = new()
    {
        [BillWorkflowAction.PrepareBatch] = [UserRole.ITEngineer, UserRole.Administrator],
        [BillWorkflowAction.Submit] = [UserRole.ITEngineer, UserRole.Administrator],
        [BillWorkflowAction.ApproveIt] = [UserRole.HeadOfIt, UserRole.Administrator],
        [BillWorkflowAction.ApproveHr] = [UserRole.GroupHrManager, UserRole.Administrator],
        [BillWorkflowAction.ApproveFinance] = [UserRole.Cfo, UserRole.Administrator],
        [BillWorkflowAction.Lock] = [UserRole.Cfo, UserRole.Administrator],
    };

    private static readonly UserRole[] ExceptionResolutionRoles = [UserRole.ITEngineer, UserRole.Administrator];

    public Task<bool> CanResolveExceptionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ExceptionResolutionRoles.Contains(currentUser.Role));

    public Task<bool> AuthorizeAsync(BillWorkflowAction action, CancellationToken cancellationToken) =>
        Task.FromResult(ActionRoles.TryGetValue(action, out var roles) && roles.Contains(currentUser.Role));
}
