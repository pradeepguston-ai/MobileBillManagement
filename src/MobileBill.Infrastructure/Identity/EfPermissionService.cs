using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MobileBill.Application.Common;
using MobileBill.Application.Identity;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Identity;

// Every request to a protected endpoint checks a permission, so the saved rows are cached; saving or resetting clears
// the cache. The short expiry keeps a second API instance (if one is ever run) from serving stale rules for long.
public sealed class EfPermissionService(MobileBillDbContext db, IMemoryCache cache, ICurrentUserService currentUser, IClock clock) : IPermissionService
{
    private const string CacheKey = "role-permissions";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(2);

    public async Task<bool> IsAllowedAsync(UserRole role, string module, PermissionAction action, CancellationToken token)
    {
        if (role == UserRole.Administrator) return true;
        var definition = PermissionCatalog.Find(module);
        if (definition is null || !definition.Supports(action)) return false;
        var saved = await LoadAsync(token);
        return Allows(Effective(definition, role, saved), action);
    }

    public async Task<IReadOnlyList<ModulePermissionDto>> GetForRoleAsync(UserRole role, CancellationToken token)
    {
        var saved = await LoadAsync(token);
        return PermissionCatalog.Modules.Select(module => Effective(module, role, saved)).ToList();
    }

    public async Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken token)
    {
        var saved = await LoadAsync(token);
        var modules = PermissionCatalog.Modules.Select(module => new PermissionModuleDto(module.Key, module.Label, module.Group, module.Note, module.Actions.Select(action => action.ToString()).ToList())).ToList();
        var roles = Enum.GetValues<UserRole>().Select(role => RoleDto(role, saved)).ToList();
        return new PermissionMatrixDto(modules, roles);
    }

    public async Task<RolePermissionsDto> SaveRoleAsync(UserRole role, SaveRolePermissionsRequest request, CancellationToken token)
    {
        EnsureEditable(role);
        if (request.Permissions is null) throw new PermissionValidationException("The permissions to save are required.");
        var unknown = request.Permissions.Select(item => item.Module).Where(key => PermissionCatalog.Find(key ?? string.Empty) is null).ToList();
        if (unknown.Count > 0) throw new PermissionValidationException($"Unknown screen: {string.Join(", ", unknown)}.");
        var duplicates = request.Permissions.GroupBy(item => item.Module).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (duplicates.Count > 0) throw new PermissionValidationException($"Each screen can be listed only once: {string.Join(", ", duplicates)}.");

        var rows = await db.RolePermissions.Where(row => row.Role == role).ToDictionaryAsync(row => row.Module, StringComparer.Ordinal, token);
        var now = clock.UtcNow;
        foreach (var item in request.Permissions)
        {
            var wanted = Normalise(PermissionCatalog.Find(item.Module)!, item);
            if (!rows.TryGetValue(item.Module, out var row))
            {
                row = new RolePermission { Role = role, Module = item.Module, CreatedAtUtc = now, CreatedBy = currentUser.UserId };
                db.RolePermissions.Add(row);
            }
            else
            {
                row.UpdatedAtUtc = now;
                row.UpdatedBy = currentUser.UserId;
            }
            (row.CanView, row.CanCreate, row.CanEdit, row.CanDelete) = (wanted.CanView, wanted.CanCreate, wanted.CanEdit, wanted.CanDelete);
        }
        await db.SaveChangesAsync(token);
        cache.Remove(CacheKey);
        return RoleDto(role, await LoadAsync(token));
    }

    public async Task<RolePermissionsDto> ResetRoleAsync(UserRole role, CancellationToken token)
    {
        EnsureEditable(role);
        var rows = await db.RolePermissions.Where(row => row.Role == role).ToListAsync(token);
        db.RolePermissions.RemoveRange(rows);
        await db.SaveChangesAsync(token);
        cache.Remove(CacheKey);
        return RoleDto(role, await LoadAsync(token));
    }

    private static void EnsureEditable(UserRole role)
    {
        if (!Enum.IsDefined(role)) throw new PermissionValidationException("A valid role is required.");
        if (role == UserRole.Administrator) throw new PermissionValidationException("The Administrator role always has full access and cannot be changed.");
    }

    private RolePermissionsDto RoleDto(UserRole role, IReadOnlyDictionary<(UserRole, string), RolePermission> saved)
    {
        var permissions = PermissionCatalog.Modules.Select(module => Effective(module, role, saved)).ToList();
        var customised = role != UserRole.Administrator && PermissionCatalog.Modules.Zip(permissions).Any(pair => pair.Second != Defaults(pair.First, role));
        return new RolePermissionsDto(role, role == UserRole.Administrator, customised, permissions);
    }

    private static ModulePermissionDto Effective(PermissionModuleDefinition module, UserRole role, IReadOnlyDictionary<(UserRole, string), RolePermission> saved)
    {
        if (role == UserRole.Administrator)
            return new(module.Key, true, module.Supports(PermissionAction.Create), module.Supports(PermissionAction.Edit), module.Supports(PermissionAction.Delete));
        return saved.TryGetValue((role, module.Key), out var row)
            ? Normalise(module, new ModulePermissionDto(module.Key, row.CanView, row.CanCreate, row.CanEdit, row.CanDelete))
            : Defaults(module, role);
    }

    private static ModulePermissionDto Defaults(PermissionModuleDefinition module, UserRole role) => new(module.Key,
        module.DefaultAllows(role, PermissionAction.View), module.DefaultAllows(role, PermissionAction.Create),
        module.DefaultAllows(role, PermissionAction.Edit), module.DefaultAllows(role, PermissionAction.Delete));

    // Actions that do not apply to the screen are off, and any change permission needs View (a screen you cannot open
    // cannot be changed from).
    private static ModulePermissionDto Normalise(PermissionModuleDefinition module, ModulePermissionDto item)
    {
        var create = item.CanCreate && module.Supports(PermissionAction.Create);
        var edit = item.CanEdit && module.Supports(PermissionAction.Edit);
        var delete = item.CanDelete && module.Supports(PermissionAction.Delete);
        return new(module.Key, item.CanView || create || edit || delete, create, edit, delete);
    }

    private static bool Allows(ModulePermissionDto permission, PermissionAction action) => action switch
    {
        PermissionAction.View => permission.CanView,
        PermissionAction.Create => permission.CanCreate,
        PermissionAction.Edit => permission.CanEdit,
        PermissionAction.Delete => permission.CanDelete,
        _ => false,
    };

    private async Task<IReadOnlyDictionary<(UserRole, string), RolePermission>> LoadAsync(CancellationToken token)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<(UserRole, string), RolePermission>? cached) && cached is not null) return cached;
        var rows = await db.RolePermissions.AsNoTracking().ToListAsync(token);
        IReadOnlyDictionary<(UserRole, string), RolePermission> loaded = rows.ToDictionary(row => (row.Role, row.Module));
        cache.Set(CacheKey, loaded, CacheLifetime);
        return loaded;
    }
}
