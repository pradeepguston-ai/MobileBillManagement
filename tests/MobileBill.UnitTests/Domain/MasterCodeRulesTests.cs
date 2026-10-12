using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class MasterCodeRulesTests
{
    [Theory]
    [InlineData(new string[0], "DEP001")]
    [InlineData(new[] { "DEP001", "DEP002" }, "DEP003")]
    [InlineData(new[] { "DEP001", "DEP007" }, "DEP008")]            // gaps are not reused
    [InlineData(new[] { "IT", "HR", "dep004", "DEPX", "DEP" }, "DEP005")]  // hand-typed codes are ignored
    [InlineData(new[] { "DEP999" }, "DEP1000")]
    public void The_next_code_follows_the_highest_running_number(string[] existing, string expected) =>
        Assert.Equal(expected, MasterCodeRules.NextCode(MasterCodeRules.Department, existing));

    [Fact]
    public void Names_compare_ignoring_case_and_repeated_spaces() =>
        Assert.Equal(MasterCodeRules.NameKey("Human Resources"), MasterCodeRules.NameKey("  human   RESOURCES "));
}
