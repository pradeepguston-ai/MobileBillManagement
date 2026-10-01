using MobileBill.Application.Common;

namespace MobileBill.Application.MasterData;

public static class MasterDataValidation
{
    public static void ValidateEffectiveDates(DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new MasterDataValidationException("Effective To must be on or after Effective From.");
        }
    }

    public static bool PeriodsOverlap(
        DateOnly firstFrom,
        DateOnly? firstTo,
        DateOnly secondFrom,
        DateOnly? secondTo) =>
        firstFrom <= (secondTo ?? DateOnly.MaxValue) && secondFrom <= (firstTo ?? DateOnly.MaxValue);

    public static void RequireText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new MasterDataValidationException($"{fieldName} is required.");
        }
    }

    public static void RequireNonNegative(decimal value, string fieldName)
    {
        if (value < 0)
        {
            throw new MasterDataValidationException($"{fieldName} cannot be negative.");
        }
    }
    public static void RequireCurrencyAmount(decimal value, string fieldName)
    {
        RequireNonNegative(value, fieldName);
        if (value > 9999999999999999.99m || decimal.Round(value, 2) != value)
        {
            throw new MasterDataValidationException($"{fieldName} must fit decimal(18,2) with no fractional cents.");
        }
    }
}
