namespace MobileBill.Domain.Calculations;

// Bill trend rules: how a month's bill compares with the same person's (or number's) earlier months.
public static class BillTrendRules
{
    public const int DefaultMonths = 12;
    public static readonly IReadOnlyList<int> AllowedMonths = [6, 12, 24];

    // A bill is "above usual" when it is more than this percentage over the average of the earlier months.
    public const decimal AboveUsualThresholdPercent = 30m;

    public static int NormalizeMonths(int? months) => months is { } value && AllowedMonths.Contains(value) ? value : DefaultMonths;

    // The average of the earlier months, to two decimal places; null when there is no history.
    public static decimal? Average(IEnumerable<decimal> earlierBills)
    {
        var values = earlierBills.ToList();
        return values.Count == 0 ? null : decimal.Round(values.Average(), 2, MidpointRounding.AwayFromZero);
    }

    // The change from the average as a percentage, to one decimal place; null when there is no average to compare with.
    public static decimal? ChangePercent(decimal current, decimal? average) =>
        average is not { } value || value <= 0m ? null : decimal.Round((current - value) / value * 100m, 1, MidpointRounding.AwayFromZero);

    public static bool IsAboveUsual(decimal current, decimal? average) =>
        ChangePercent(current, average) is { } change && change > AboveUsualThresholdPercent;

    // Over the entitlement (credit limit + rental): the same condition that gives a calculated excess.
    public static bool IsOverLimit(decimal actualBill, decimal creditLimit, decimal monthlyRental) => actualBill > creditLimit + monthlyRental;
}
