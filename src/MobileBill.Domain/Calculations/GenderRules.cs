using MobileBill.Domain.Enums;

namespace MobileBill.Domain.Calculations;

// The title shown before an employee's name follows from their gender, so the two can never disagree.
public static class GenderRules
{
    public static string Title(Gender gender) => gender == Gender.Male ? "MR." : "MS.";

    public static string? Title(Gender? gender) => gender is { } known ? Title(known) : null;

    // Gender as people write it in a sheet: Male / M / Mr. or Female / F / Ms., in any case.
    public static Gender? Parse(string? text) => new string((text ?? string.Empty).Where(char.IsLetter).ToArray()).ToUpperInvariant() switch
    {
        "MALE" or "M" or "MR" => Gender.Male,
        "FEMALE" or "F" or "MS" => Gender.Female,
        _ => null,
    };
}
