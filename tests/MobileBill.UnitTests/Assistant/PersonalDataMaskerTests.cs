using MobileBill.Application.Assistant;

namespace MobileBill.UnitTests.Assistant;

public sealed class PersonalDataMaskerTests
{
    [Fact]
    public void The_same_value_keeps_its_placeholder_and_each_kind_counts_separately()
    {
        var masker = new PersonalDataMasker();

        Assert.Equal("PERSON-1", masker.Person("Sam Perera"));
        Assert.Equal("PERSON-1", masker.Person("  sam   PERERA "));
        Assert.Equal("PERSON-2", masker.Person("Nimal Silva"));
        Assert.Equal("EPF-1", masker.Epf("211464"));
        Assert.Equal("MOBILE-1", masker.Mobile("761499198"));
        Assert.Equal("MOBILE-1", masker.Mobile("0761499198"));   // the same number with a leading 0
        Assert.Equal("MOBILE-1", masker.Mobile("+94761499198"));
        Assert.Equal("", masker.Person(null));
    }

    [Fact]
    public void Free_text_is_masked_and_the_answer_unmasked()
    {
        var masker = new PersonalDataMasker();
        masker.Person("Sam Perera");

        var masked = masker.MaskText("Why is sam perera's bill on 0761499198 so high? His EPF is 211464.",
            [(PersonalDataKind.Mobile, "761499198"), (PersonalDataKind.Epf, "211464")]);

        Assert.Equal("Why is PERSON-1's bill on MOBILE-1 so high? His EPF is EPF-1.", masked);
        Assert.Equal("Sam Perera (761499198, EPF 211464) went over by LKR 2,800.00.", masker.Unmask("PERSON-1 (MOBILE-1, EPF EPF-1) went over by LKR 2,800.00."));
        Assert.Equal("PERSON-9 is unknown", masker.Unmask("PERSON-9 is unknown"));
    }

    [Fact]
    public void Masking_matches_whole_values_only()
    {
        var masker = new PersonalDataMasker();
        masker.Epf("2114");

        Assert.Equal("Bill 211464, EPF EPF-1, total 12114", masker.MaskText("Bill 211464, EPF 2114, total 12114", []));
        Assert.Equal("EPF-1 and MOBILE-12", masker.MaskText("2114 and MOBILE-12", []));
    }

    [Theory]
    [InlineData("761499198", "761499198")]
    [InlineData("0761499198", "761499198")]
    [InlineData("+94 76 149 9198", "761499198")]
    [InlineData("076-149-9198", "761499198")]
    public void Mobile_numbers_are_normalized_to_how_they_are_stored(string written, string stored) =>
        Assert.Equal(stored, PersonalDataMasker.NormalizeMobile(written));
}
