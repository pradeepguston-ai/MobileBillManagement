namespace MobileBill.Domain.Calculations;

// A resignation dated after today is pending: the employee stays active and keeps their numbers until that day.
// On the day itself (or once it has passed) the resignation is completed: the employee becomes inactive and
// their numbers go to the SIM Pool.
public static class ResignationRules
{
    public static bool IsPending(DateOnly resignedOn, DateOnly today) => resignedOn > today;

    public static bool IsDue(DateOnly resignedOn, DateOnly today) => !IsPending(resignedOn, today);

    // Days left until the last day; 0 once it is due.
    public static int DaysLeft(DateOnly resignedOn, DateOnly today) => Math.Max(0, resignedOn.DayNumber - today.DayNumber);
}
