using MobileBill.Domain.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Entities;

// One role's access to one screen, as set by an administrator in User Management. A role and screen with no row
// uses the built-in default (today's access rules). The Administrator role always has full access and is never stored.
public sealed class RolePermission : AuditableEntity
{
    public UserRole Role { get; set; }
    public required string Module { get; set; }
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
