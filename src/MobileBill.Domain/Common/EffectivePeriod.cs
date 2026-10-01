namespace MobileBill.Domain.Common;

public readonly record struct EffectivePeriod
{
    public EffectivePeriod(DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("EffectiveTo cannot be earlier than EffectiveFrom.", nameof(effectiveTo));
        }

        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public DateOnly EffectiveFrom { get; }

    public DateOnly? EffectiveTo { get; }

    public bool Overlaps(EffectivePeriod other) =>
        EffectiveFrom <= other.EffectiveToOrMaximum && other.EffectiveFrom <= EffectiveToOrMaximum;

    private DateOnly EffectiveToOrMaximum => EffectiveTo ?? DateOnly.MaxValue;
}
