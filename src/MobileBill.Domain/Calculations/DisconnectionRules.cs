namespace MobileBill.Domain.Calculations;

// After a SIM is disconnected the provider still sends one final bill. Bills for the month of disconnection and up to
// FinalBillGraceMonths after it are that final bill: charged to the last holder against their normal entitlement.
// A bill for any later month means the provider kept billing a disconnected number and is flagged for follow-up.
public static class DisconnectionRules
{
    public const int FinalBillGraceMonths = 1;

    public static bool IsFinalBill(DateOnly disconnectedOn, int billingYear, int billingMonth) =>
        billingYear * 12 + billingMonth - (disconnectedOn.Year * 12 + disconnectedOn.Month) <= FinalBillGraceMonths;
}
