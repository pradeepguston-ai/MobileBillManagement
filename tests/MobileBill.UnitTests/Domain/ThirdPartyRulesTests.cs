using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class ThirdPartyRulesTests
{
    [Theory]
    [InlineData("OTHER - THIRD-PARTY", true)]
    [InlineData(" other  -  third-party ", true)]
    [InlineData("EXECUTIVE", false)]
    [InlineData("GENERAL EMPLOYEE", false)]
    [InlineData(null, false)]
    public void Only_the_third_party_category_takes_a_third_party_company(string? category, bool expected) =>
        Assert.Equal(expected, ThirdPartyRules.AllowsThirdParty(category));
}
