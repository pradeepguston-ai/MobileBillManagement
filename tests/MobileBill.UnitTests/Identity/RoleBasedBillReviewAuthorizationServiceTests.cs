using MobileBill.Api.Identity;
using MobileBill.Application.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.UnitTests.Identity;

public sealed class RoleBasedBillReviewAuthorizationServiceTests
{
    [Theory]
    [InlineData(BillWorkflowAction.PrepareBatch, UserRole.ITEngineer, true)]
    [InlineData(BillWorkflowAction.PrepareBatch, UserRole.Administrator, true)]
    [InlineData(BillWorkflowAction.PrepareBatch, UserRole.HeadOfIt, false)]
    [InlineData(BillWorkflowAction.Submit, UserRole.ITEngineer, true)]
    [InlineData(BillWorkflowAction.Submit, UserRole.HeadOfIt, false)]
    [InlineData(BillWorkflowAction.ApproveIt, UserRole.HeadOfIt, true)]
    [InlineData(BillWorkflowAction.ApproveIt, UserRole.ITEngineer, false)]
    [InlineData(BillWorkflowAction.ApproveIt, UserRole.Administrator, true)]
    [InlineData(BillWorkflowAction.ApproveHr, UserRole.GroupHrManager, true)]
    [InlineData(BillWorkflowAction.ApproveHr, UserRole.HeadOfIt, false)]
    [InlineData(BillWorkflowAction.ApproveFinance, UserRole.Cfo, true)]
    [InlineData(BillWorkflowAction.ApproveFinance, UserRole.GroupHrManager, false)]
    [InlineData(BillWorkflowAction.Lock, UserRole.Cfo, true)]
    [InlineData(BillWorkflowAction.Lock, UserRole.Administrator, true)]
    [InlineData(BillWorkflowAction.Lock, UserRole.ITEngineer, false)]
    [InlineData(BillWorkflowAction.Reject, UserRole.Administrator, false)]
    public async Task AuthorizeAsync_matches_the_expected_role_for_each_action(BillWorkflowAction action, UserRole role, bool expected)
    {
        var service = new RoleBasedBillReviewAuthorizationService(new FixedRoleUser(role));

        Assert.Equal(expected, await service.AuthorizeAsync(action, default));
    }

    [Theory]
    [InlineData(UserRole.ITEngineer, true)]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.HeadOfIt, false)]
    [InlineData(UserRole.GroupHrManager, false)]
    [InlineData(UserRole.Cfo, false)]
    public async Task CanResolveExceptionsAsync_is_granted_only_to_it_engineer_and_administrator(UserRole role, bool expected)
    {
        var service = new RoleBasedBillReviewAuthorizationService(new FixedRoleUser(role));

        Assert.Equal(expected, await service.CanResolveExceptionsAsync(default));
    }

    private sealed class FixedRoleUser(UserRole role) : ICurrentUserService
    {
        public string UserId => "test-user";
        public string DisplayName => "Test User";
        public UserRole Role => role;
    }
}
