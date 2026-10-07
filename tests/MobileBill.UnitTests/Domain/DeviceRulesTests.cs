using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class DeviceRulesTests
{
    [Theory]
    [InlineData("2026-03-01", "2026-03-01", 0)]
    [InlineData("2026-03-01", "2026-03-31", 0)]
    [InlineData("2026-03-01", "2026-04-01", 1)]
    [InlineData("2025-10-07", "2026-10-07", 12)]
    [InlineData("2025-10-08", "2026-10-07", 11)]    // the 12th month is not complete until the 8th
    [InlineData("2026-10-07", "2026-01-01", 0)]     // a date before purchase counts as new
    public void Months_used_count_whole_months_from_purchase(string purchased, string on, int expected) =>
        Assert.Equal(expected, DeviceRules.MonthsUsed(DateOnly.Parse(purchased), DateOnly.Parse(on)));

    [Theory]
    [InlineData(85000, "2025-10-07", "2026-10-07", 63750)]      // 36 of 48 months left
    [InlineData(85000, "2026-10-07", "2026-10-07", 85000)]
    [InlineData(85000, "2025-04-07", "2026-10-07", 53125)]      // 18 months used: 30 of 48 left
    [InlineData(85000, "2023-10-07", "2026-10-07", 21250)]      // 36 months used: 12 of 48 left
    [InlineData(85000, "2022-10-07", "2026-10-07", 0)]          // fully written off after 48 months
    [InlineData(85000, "2020-01-01", "2026-10-07", 0)]
    [InlineData(0, "2026-01-01", "2026-10-07", 0)]
    public void Recoverable_amount_is_the_straight_line_value_left(decimal cost, string purchased, string on, decimal expected) =>
        Assert.Equal(expected, DeviceRules.RecoverableAmount(cost, DateOnly.Parse(purchased), DateOnly.Parse(on)));

    [Theory]
    [InlineData("2026-09-23", false)]   // 14 days: still within the grace period
    [InlineData("2026-09-22", true)]    // 15 days
    [InlineData("2026-10-07", false)]
    public void Collection_is_overdue_after_14_days(string since, bool overdue) =>
        Assert.Equal(overdue, DeviceRules.IsCollectionOverdue(DateOnly.Parse(since), new DateOnly(2026, 10, 7)));

    [Theory]
    [InlineData("356789010000014", true)]
    [InlineData("490154203237518", true)]
    [InlineData("356789010000015", false)]   // wrong check digit
    [InlineData("35678901000001", false)]    // 14 digits
    [InlineData("35678901000001A", false)]
    [InlineData(null, false)]
    public void Imei_must_be_15_digits_with_a_valid_check_digit(string? imei, bool valid) =>
        Assert.Equal(valid, DeviceRules.IsValidImei(imei));
}
