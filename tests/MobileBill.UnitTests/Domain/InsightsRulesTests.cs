using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class InsightsRulesTests
{
    [Theory]
    [InlineData(0, "within")]
    [InlineData(0.01, "upTo500")]
    [InlineData(500, "upTo500")]
    [InlineData(500.01, "upTo2000")]
    [InlineData(2000, "upTo2000")]
    [InlineData(5000, "upTo5000")]
    [InlineData(5000.01, "over5000")]
    [InlineData(250000, "over5000")]
    public void Excess_falls_in_the_band_whose_upper_bound_it_does_not_pass(decimal excess, string band) =>
        Assert.Equal(band, InsightsRules.ExcessBands[InsightsRules.ExcessBand(excess)].Key);

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    public void Three_months_over_limit_makes_a_repeat(int monthsOver, bool expected) =>
        Assert.Equal(expected, InsightsRules.IsRepeatOverLimit(monthsOver));

    [Fact]
    public void Periods_run_on_across_a_year_end() =>
        Assert.Equal(1, InsightsRules.Period(2027, 1) - InsightsRules.Period(2026, 12));
}
