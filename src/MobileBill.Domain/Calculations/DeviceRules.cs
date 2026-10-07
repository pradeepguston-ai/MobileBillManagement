namespace MobileBill.Domain.Calculations;

// Company mobile device rules.
public static class DeviceRules
{
    // A device's value is written off evenly over this many months from its purchase date.
    public const int UsefulLifeMonths = 48;

    // Days a leaver's device may wait to be collected before it is overdue and its value shown for recovery.
    public const int CollectionGraceDays = 14;

    // Whole months between purchase and the given date (a month counts once the same day of the month is reached).
    public static int MonthsUsed(DateOnly purchasedOn, DateOnly onDate)
    {
        if (onDate <= purchasedOn) return 0;
        var months = (onDate.Year - purchasedOn.Year) * 12 + onDate.Month - purchasedOn.Month;
        if (onDate.Day < purchasedOn.Day) months--;
        return Math.Max(0, months);
    }

    // The depreciated (straight-line) value still left on the given date, to two decimal places; 0 once fully written off.
    public static decimal RecoverableAmount(decimal purchaseCost, DateOnly purchasedOn, DateOnly onDate)
    {
        if (purchaseCost <= 0m) return 0m;
        var monthsLeft = Math.Max(0, UsefulLifeMonths - MonthsUsed(purchasedOn, onDate));
        return decimal.Round(purchaseCost * monthsLeft / UsefulLifeMonths, 2, MidpointRounding.AwayFromZero);
    }

    public static int DaysWaiting(DateOnly since, DateOnly today) => Math.Max(0, today.DayNumber - since.DayNumber);

    public static bool IsCollectionOverdue(DateOnly returnPendingSince, DateOnly today) => DaysWaiting(returnPendingSince, today) > CollectionGraceDays;

    // An IMEI is 15 digits with a Luhn check digit.
    public static bool IsValidImei(string? imei)
    {
        if (imei is null || imei.Length != 15 || !imei.All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var index = 0; index < 15; index++)
        {
            var digit = imei[index] - '0';
            if (index % 2 == 1) { digit *= 2; if (digit > 9) digit -= 9; }
            sum += digit;
        }
        return sum % 10 == 0;
    }
}
