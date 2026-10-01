using MobileBill.Domain.Common;

namespace MobileBill.UnitTests.Domain;

public sealed class EffectivePeriodTests
{
    [Fact]
    public void Overlaps_returns_true_when_an_open_ended_period_contains_the_other_period()
    {
        var allocation = new EffectivePeriod(new DateOnly(2026, 1, 1), null);
        var reassignment = new EffectivePeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        var overlaps = allocation.Overlaps(reassignment);

        Assert.True(overlaps);
    }

    [Fact]
    public void Overlaps_returns_false_when_periods_are_separated()
    {
        var formerAllocation = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 31));
        var reassignment = new EffectivePeriod(new DateOnly(2026, 8, 1), null);

        var overlaps = formerAllocation.Overlaps(reassignment);

        Assert.False(overlaps);
    }

    [Fact]
    public void Constructor_rejects_an_end_date_before_the_start_date()
    {
        Action create = () => _ = new EffectivePeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 31));

        Assert.Throws<ArgumentException>(create);
    }
}
