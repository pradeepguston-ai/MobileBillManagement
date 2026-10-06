using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class BillTrendRulesTests
{
    [Theory]
    [InlineData(null, 12)]
    [InlineData(6, 6)]
    [InlineData(24, 24)]
    [InlineData(7, 12)]
    [InlineData(0, 12)]
    public void Months_are_limited_to_6_12_or_24(int? requested, int expected) => Assert.Equal(expected, BillTrendRules.NormalizeMonths(requested));

    [Fact]
    public void Average_is_rounded_to_two_places_and_null_without_history()
    {
        Assert.Equal(1000.33m, BillTrendRules.Average([1000m, 1000m, 1001m]));
        Assert.Null(BillTrendRules.Average([]));
    }

    [Theory]
    [InlineData(1300, 1000, 30.0)]
    [InlineData(750, 1000, -25.0)]
    [InlineData(1000.5, 3000, -66.7)]
    public void Change_is_a_percentage_of_the_average(decimal current, decimal average, decimal expected) =>
        Assert.Equal(expected, BillTrendRules.ChangePercent(current, average));

    [Fact]
    public void Change_is_null_without_a_positive_average()
    {
        Assert.Null(BillTrendRules.ChangePercent(500m, null));
        Assert.Null(BillTrendRules.ChangePercent(500m, 0m));
    }

    [Theory]
    [InlineData(1300, 1000, false)]   // exactly 30% is still usual
    [InlineData(1301, 1000, true)]
    public void Above_usual_means_more_than_30_percent_over_the_average(decimal current, decimal average, bool expected) =>
        Assert.Equal(expected, BillTrendRules.IsAboveUsual(current, average));

    [Fact]
    public void First_month_is_never_above_usual() => Assert.False(BillTrendRules.IsAboveUsual(500m, null));

    [Theory]
    [InlineData(1500, 1000, 500, false)]
    [InlineData(1500.01, 1000, 500, true)]
    public void Over_limit_means_above_credit_limit_plus_rental(decimal actual, decimal limit, decimal rental, bool expected) =>
        Assert.Equal(expected, BillTrendRules.IsOverLimit(actual, limit, rental));
}
