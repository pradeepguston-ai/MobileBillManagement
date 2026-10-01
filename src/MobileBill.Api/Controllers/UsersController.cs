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

    [HttpGet]
    public Task<IReadOnlyList<ManagedUserDto>> List([FromQuery] string? search, CancellationToken token) => userManagement.GetUsersAsync(search, token);

    [HttpPut("{id:guid}/role")]
    public Task<UserDto> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken token) => userManagement.ChangeRoleAsync(id, request, token);

    [HttpPost("{id:guid}/activate")]
    public Task<UserDto> Activate(Guid id, ActivateUserRequest request, CancellationToken token) => userManagement.ActivateUserAsync(id, request, token);

    [HttpPost("{id:guid}/deactivate")]
    public Task<UserDto> Deactivate(Guid id, CancellationToken token) => userManagement.DeactivateUserAsync(id, token);

    [HttpGet("password-resets")]
    public Task<IReadOnlyList<PasswordResetRequestDto>> PasswordResets(CancellationToken token) => userManagement.GetPendingPasswordResetsAsync(token);

    [HttpPost("password-resets/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePasswordReset(Guid id, CancellationToken token)
    {
        await userManagement.ApprovePasswordResetAsync(id, token);
        return NoContent();
    }

    [HttpPost("password-resets/{id:guid}/reject")]
    public async Task<IActionResult> RejectPasswordReset(Guid id, CancellationToken token)
    {
        await userManagement.RejectPasswordResetAsync(id, token);
        return NoContent();
    }
}
