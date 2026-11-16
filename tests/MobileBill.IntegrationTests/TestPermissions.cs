using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Identity;
using MobileBill.Domain.Enums;

namespace MobileBill.IntegrationTests;

// For test hosts whose database has no RolePermissions table (hand-made SQLite schemas) or no database at all (fake
// services): every role gets the built-in default permissions. PermissionsApiTests covers the saved permissions.
internal static class TestPermissions
{
    public static IServiceCollection UseDefaultPermissions(this IServiceCollection services)
    {
        services.RemoveAll<IPermissionService>();
        services.AddSingleton<IPermissionService, DefaultPermissions>();
        return services;
    }

    private sealed class DefaultPermissions : IPermissionService
    {
        public Task<bool> IsAllowedAsync(UserRole role, string module, PermissionAction action, CancellationToken cancellationToken) =>
            Task.FromResult(PermissionCatalog.DefaultAllows(role, module, action));

        public Task<IReadOnlyList<ModulePermissionDto>> GetForRoleAsync(UserRole role, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModulePermissionDto>>(PermissionCatalog.Modules.Select(module => new ModulePermissionDto(module.Key,
                PermissionCatalog.DefaultAllows(role, module.Key, PermissionAction.View), PermissionCatalog.DefaultAllows(role, module.Key, PermissionAction.Create),
                PermissionCatalog.DefaultAllows(role, module.Key, PermissionAction.Edit), PermissionCatalog.DefaultAllows(role, module.Key, PermissionAction.Delete))).ToList());

        public Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RolePermissionsDto> SaveRoleAsync(UserRole role, SaveRolePermissionsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RolePermissionsDto> ResetRoleAsync(UserRole role, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
