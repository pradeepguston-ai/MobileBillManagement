using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MobileBill.Application.Identity;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Identity;

// [Permission(PermissionModules.Employees, PermissionAction.Edit)] lets a request through only when the caller's role has
// that permission in User Management > Permissions (Administrator always does). Several on one action must all pass.
public sealed class PermissionAttribute(string module, PermissionAction action) : AuthorizeAttribute(PolicyName(module, action))
{
    public const string PolicyPrefix = "Permission:";

    public static string PolicyName(string module, PermissionAction action) => $"{PolicyPrefix}{module}:{action}";

    public static bool TryParse(string policyName, out PermissionRequirement requirement)
    {
        requirement = null!;
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal)) return false;
        var parts = policyName[PolicyPrefix.Length..].Split(':');
        if (parts.Length != 2 || !Enum.TryParse<PermissionAction>(parts[1], out var action)) return false;
        requirement = new PermissionRequirement(parts[0], action);
        return true;
    }
}

public sealed record PermissionRequirement(string Module, PermissionAction Action) : IAuthorizationRequirement;

// Builds the "Permission:module:action" policies on demand; every other policy name goes to the default provider.
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        PermissionAttribute.TryParse(policyName, out var requirement)
            ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(requirement).Build()
            : await base.GetPolicyAsync(policyName);
}

public sealed class PermissionHandler(IPermissionService permissions, IHttpContextAccessor accessor) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!Enum.TryParse<UserRole>(context.User.FindFirstValue(ClaimTypes.Role), out var role)) return;
        var token = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (await permissions.IsAllowedAsync(role, requirement.Module, requirement.Action, token)) context.Succeed(requirement);
    }
}
