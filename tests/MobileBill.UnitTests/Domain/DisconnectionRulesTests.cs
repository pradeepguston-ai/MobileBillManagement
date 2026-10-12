using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class DisconnectionRulesTests
{
    [Theory]
    [InlineData("2026-10-12", 2026, 9, true)]    // bill for a month before the disconnection
    [InlineData("2026-10-12", 2026, 10, true)]   // month of disconnection
    [InlineData("2026-10-12", 2026, 11, true)]   // one month after: the final bill
    [InlineData("2026-10-12", 2026, 12, false)]  // two months after: should no longer be billed
    [InlineData("2026-12-31", 2027, 1, true)]    // across the year end
    [InlineData("2026-11-01", 2027, 1, false)]
    public void Final_bill_is_the_month_of_disconnection_and_one_month_after(string disconnectedOn, int year, int month, bool expected) =>
        Assert.Equal(expected, DisconnectionRules.IsFinalBill(DateOnly.Parse(disconnectedOn), year, month));
}
