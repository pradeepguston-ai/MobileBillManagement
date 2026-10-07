using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class ResignationRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);

    [Theory]
    [InlineData(2026, 10, 8, true, false, 1)]
    [InlineData(2026, 10, 31, true, false, 24)]
    [InlineData(2026, 10, 7, false, true, 0)]     // the date itself completes the resignation
    [InlineData(2026, 9, 30, false, true, 0)]
    public void Only_a_date_after_today_is_pending(int year, int month, int day, bool pending, bool due, int daysLeft)
    {
        var resignedOn = new DateOnly(year, month, day);
        Assert.Equal((pending, due, daysLeft), (ResignationRules.IsPending(resignedOn, Today), ResignationRules.IsDue(resignedOn, Today), ResignationRules.DaysLeft(resignedOn, Today)));
    }
}
