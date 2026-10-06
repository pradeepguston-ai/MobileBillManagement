using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class VasRulesTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void A_repeat_user_has_vas_in_at_least_two_of_the_last_three_months(int monthsWithVas, bool expected) =>
        Assert.Equal(expected, VasRules.IsRepeatUser(monthsWithVas));

    [Theory]
    [InlineData(2855.00, 4468.45, 63.9)]
    [InlineData(100, 400, 25.0)]
    [InlineData(1, 3, 33.3)]
    [InlineData(50, 0, 0)]
    [InlineData(50, -10, 0)]
    public void Share_of_bill_is_a_percentage_to_one_decimal(decimal vas, decimal actualBill, decimal expected) =>
        Assert.Equal(expected, VasRules.ShareOfBill(vas, actualBill));

    [Theory]
    [InlineData(0.01, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    public void Only_a_positive_vas_charge_counts(decimal vas, bool expected) =>
        Assert.Equal(expected, VasRules.HasVas(vas));
}
