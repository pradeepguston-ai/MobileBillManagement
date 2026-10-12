using MobileBill.Domain.Calculations;
using MobileBill.Domain.Enums;

namespace MobileBill.UnitTests.Domain;

public sealed class GenderRulesTests
{
    [Fact]
    public void The_title_follows_the_gender()
    {
        Assert.Equal("MR.", GenderRules.Title(Gender.Male));
        Assert.Equal("MS.", GenderRules.Title(Gender.Female));
        Assert.Null(GenderRules.Title((Gender?)null));
    }

    [Theory]
    [InlineData("Male", Gender.Male)]
    [InlineData(" m ", Gender.Male)]
    [InlineData("MR.", Gender.Male)]
    [InlineData("female", Gender.Female)]
    [InlineData("F", Gender.Female)]
    [InlineData("Ms.", Gender.Female)]
    [InlineData("Other", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Gender_is_read_from_how_people_write_it(string? text, Gender? expected) =>
        Assert.Equal(expected, GenderRules.Parse(text));
}
