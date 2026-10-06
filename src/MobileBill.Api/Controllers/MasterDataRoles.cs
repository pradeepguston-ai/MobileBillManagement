using MobileBill.Domain.Enums;

namespace MobileBill.Api.Controllers;

public static class MasterDataRoles
{
    // Full control, including telecom providers and mobile allocation amounts.
    public const string Editors = nameof(UserRole.ITEngineer) + "," + nameof(UserRole.Administrator);

    // Employee and organisation masters (employees, factories, departments, sections, sub sections, designations,
    // categories), and moving a mobile number to another employee. HR and Finance users get these but not providers,
    // new allocations, or credit limit / rental changes.
    public const string MasterEditors = Editors + "," + nameof(UserRole.HrUser) + "," + nameof(UserRole.FinanceUser);
}
