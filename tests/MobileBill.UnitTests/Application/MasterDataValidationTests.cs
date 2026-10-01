using MobileBill.Application.MasterData;
using MobileBill.Application.Common;

namespace MobileBill.UnitTests.Application;

public sealed class MasterDataValidationTests
{
    [Fact]
    public void ValidateEffectiveDates_rejects_an_end_date_before_the_start_date()
    {
        var exception = Assert.Throws<MasterDataValidationException>(() =>
            MasterDataValidation.ValidateEffectiveDates(new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 31)));

        Assert.Equal("Effective To must be on or after Effective From.", exception.Message);
    }

    [Fact]
    public void PeriodsOverlap_treats_an_open_ended_period_as_overlapping()
    {
        var overlaps = MasterDataValidation.PeriodsOverlap(
            new DateOnly(2026, 1, 1), null,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.True(overlaps);
    }
}
