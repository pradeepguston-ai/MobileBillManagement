using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Identity;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Controllers;

[ApiController, Route("api/users"), Authorize(Roles = nameof(UserRole.Administrator))]
public sealed class UsersController(IUserManagementService userManagement) : ControllerBase
{
    [HttpGet("pending")]
    public Task<IReadOnlyList<PendingUserDto>> Pending(CancellationToken token) => userManagement.GetPendingUsersAsync(token);

    [HttpPost("{id:guid}/activate")]
    public Task<UserDto> Activate(Guid id, ActivateUserRequest request, CancellationToken token) => userManagement.ActivateUserAsync(id, request, token);

    [HttpPost("{id:guid}/deactivate")]
    public Task<UserDto> Deactivate(Guid id, CancellationToken token) => userManagement.DeactivateUserAsync(id, token);
}
