namespace MobileBill.Domain.Calculations;

// Value Added Services (VAS) report rules.
public static class VasRules
{
    // How many billing months (the reported month and the ones before it) are looked at for repeat VAS use.
    public const int RepeatWindowMonths = 3;

    // A number is a repeat VAS user when it has VAS in at least this many of those months.
    public const int RepeatMinimumMonths = 2;

    public static bool HasVas(decimal valueAddedServices) => valueAddedServices > 0m;

    public static bool IsRepeatUser(int monthsWithVas) => monthsWithVas >= RepeatMinimumMonths;

    // VAS as a percentage of the actual bill, to one decimal place; 0 when there is no bill to compare with.
    public static decimal ShareOfBill(decimal valueAddedServices, decimal actualBill) =>
        actualBill <= 0m ? 0m : decimal.Round(valueAddedServices / actualBill * 100m, 1, MidpointRounding.AwayFromZero);
}
