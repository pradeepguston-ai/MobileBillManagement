namespace MobileBill.Domain.Enums;

// What a role may do on a screen. The system deactivates records rather than deleting them, so Delete means Deactivate.
public enum PermissionAction
{
    View = 0,
    Create = 1,
    Edit = 2,
    Delete = 3,
}
