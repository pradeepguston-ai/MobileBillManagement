using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Api.Identity;
using MobileBill.Application.Identity;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Controllers;

// Access follows the User Management permission (User Management > Permissions); the service keeps Administrator
// accounts and the Administrator role for Administrators.
[ApiController, Route("api/users"), Authorize]
public sealed class UsersController(IUserManagementService userManagement) : ControllerBase
{
    [HttpGet("pending"), Permission(PermissionModules.UserManagement, PermissionAction.View)]
    public Task<IReadOnlyList<PendingUserDto>> Pending(CancellationToken token) => userManagement.GetPendingUsersAsync(token);

    [HttpGet, Permission(PermissionModules.UserManagement, PermissionAction.View)]
    public Task<IReadOnlyList<ManagedUserDto>> List([FromQuery] string? search, CancellationToken token) => userManagement.GetUsersAsync(search, token);

    [HttpPost, Permission(PermissionModules.UserManagement, PermissionAction.Create)]
    public Task<ManagedUserDto> Create(CreateUserRequest request, CancellationToken token) => userManagement.CreateUserAsync(request, token);

    [HttpPut("{id:guid}/role"), Permission(PermissionModules.UserManagement, PermissionAction.Edit)]
    public Task<UserDto> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken token) => userManagement.ChangeRoleAsync(id, request, token);

    [HttpPost("{id:guid}/activate"), Permission(PermissionModules.UserManagement, PermissionAction.Edit)]
    public Task<UserDto> Activate(Guid id, ActivateUserRequest request, CancellationToken token) => userManagement.ActivateUserAsync(id, request, token);

    [HttpPost("{id:guid}/deactivate"), Permission(PermissionModules.UserManagement, PermissionAction.Delete)]
    public Task<UserDto> Deactivate(Guid id, CancellationToken token) => userManagement.DeactivateUserAsync(id, token);

    [HttpGet("password-resets"), Permission(PermissionModules.UserManagement, PermissionAction.View)]
    public Task<IReadOnlyList<PasswordResetRequestDto>> PasswordResets(CancellationToken token) => userManagement.GetPendingPasswordResetsAsync(token);

    [HttpPost("password-resets/{id:guid}/approve"), Permission(PermissionModules.UserManagement, PermissionAction.Edit)]
    public async Task<IActionResult> ApprovePasswordReset(Guid id, CancellationToken token)
    {
        await userManagement.ApprovePasswordResetAsync(id, token);
        return NoContent();
    }

    [HttpPost("password-resets/{id:guid}/reject"), Permission(PermissionModules.UserManagement, PermissionAction.Edit)]
    public async Task<IActionResult> RejectPasswordReset(Guid id, CancellationToken token)
    {
        await userManagement.RejectPasswordResetAsync(id, token);
        return NoContent();
    }
}
