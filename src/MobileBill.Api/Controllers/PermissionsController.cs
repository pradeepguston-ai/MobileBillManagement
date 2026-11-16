using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Api.Identity;
using MobileBill.Application.Common;
using MobileBill.Application.Identity;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Controllers;

// Which role may open and change which screen. Administrators, and roles given Role Permissions, manage it; every
// signed-in user reads their own.
[ApiController, Route("api/permissions"), Authorize]
public sealed class PermissionsController(IPermissionService permissions, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("mine")]
    public Task<IReadOnlyList<ModulePermissionDto>> Mine(CancellationToken token) => permissions.GetForRoleAsync(currentUser.Role, token);

    [HttpGet, Permission(PermissionModules.RolePermissions, PermissionAction.View)]
    public Task<PermissionMatrixDto> Matrix(CancellationToken token) => permissions.GetMatrixAsync(token);

    [HttpPut("roles/{role}"), Permission(PermissionModules.RolePermissions, PermissionAction.Edit)]
    public Task<RolePermissionsDto> Save(UserRole role, SaveRolePermissionsRequest request, CancellationToken token) => permissions.SaveRoleAsync(role, request, token);

    [HttpPost("roles/{role}/reset"), Permission(PermissionModules.RolePermissions, PermissionAction.Edit)]
    public Task<RolePermissionsDto> Reset(UserRole role, CancellationToken token) => permissions.ResetRoleAsync(role, token);
}
