using MobileBill.Domain.Enums;

namespace MobileBill.Api.Controllers;

public static class MasterDataRoles
{
    // Full control, including telecom providers, mobile packages and every change to mobile allocations.
    public const string Editors = nameof(UserRole.ITEngineer) + "," + nameof(UserRole.Administrator);

    // Employee and organisation masters (employees, including Resign, factories, departments, sections, sub sections,
    // designations, categories). HR and Finance users get these; providers, packages and allocations are view-only for them.
    public const string MasterEditors = Editors + "," + nameof(UserRole.HrUser) + "," + nameof(UserRole.FinanceUser);
}
