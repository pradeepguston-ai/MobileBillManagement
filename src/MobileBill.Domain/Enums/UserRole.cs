namespace MobileBill.Domain.Enums;

public enum UserRole
{
    Administrator,
    ITEngineer,
    HeadOfIt,
    GroupHrManager,
    Cfo,
    // Master-data maintainers: edit employee and organisation masters, reassign mobile numbers,
    // and view billing and download reports. No billing changes or approvals.
    HrUser,
    FinanceUser
}
