using MobileBill.Domain.Enums;

namespace MobileBill.Api.Controllers;

public static class MasterDataRoles
{
    public const string Editors = nameof(UserRole.ITEngineer) + "," + nameof(UserRole.Administrator);
}
