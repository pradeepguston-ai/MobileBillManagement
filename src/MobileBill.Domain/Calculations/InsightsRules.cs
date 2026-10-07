namespace MobileBill.Domain.Calculations;

// Rules behind the Dashboard charts.
public static class InsightsRules
{
    // A number over its limit in at least RepeatOverLimitMonths of the last RepeatWindowMonths billing months is a repeat over-limit number.
    public const int RepeatWindowMonths = 6;
    public const int RepeatOverLimitMonths = 3;

    // Calculated excess bands for the bill range chart; each upper bound is inclusive and the last band is open-ended.
    public static readonly IReadOnlyList<(string Key, string Label, decimal? UpTo)> ExcessBands =
    [
        ("within", "Within limit", 0m),
        ("upTo500", "Up to 500", 500m),
        ("upTo2000", "500 – 2,000", 2000m),
        ("upTo5000", "2,000 – 5,000", 5000m),
        ("over5000", "Over 5,000", null),
    ];

    public static int ExcessBand(decimal calculatedExcess)
    {
        for (var index = 0; index < ExcessBands.Count; index++)
            if (ExcessBands[index].UpTo is not { } upTo || calculatedExcess <= upTo) return index;
        return ExcessBands.Count - 1;
    }

    public static bool IsRepeatOverLimit(int monthsOverLimit) => monthsOverLimit >= RepeatOverLimitMonths;

    // Billing periods as a running month number, so "the last six months" is a simple range.
    public static int Period(int year, int month) => year * 12 + month;
}
