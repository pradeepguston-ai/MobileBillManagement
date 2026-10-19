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
    FinanceUser,
    // Read-only: sees every master, billing screen, report and the dashboard, but is in no role list that allows a
    // change, so every create, edit, upload, assessment, approval and lock is refused. Suited to a shared account.
    Viewer
}
