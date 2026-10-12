namespace MobileBill.Domain.Calculations;

// A third-party allocation (the outside company an employee comes from) is recorded only for employees in the
// OTHER - THIRD-PARTY category.
public static class ThirdPartyRules
{
    public const string ThirdPartyCategoryName = "OTHER - THIRD-PARTY";

    // Written in an import sheet for no company.
    public const string NotApplicable = "NOT APPLICABLE";

    public static bool AllowsThirdParty(string? categoryName) =>
        categoryName is not null && MasterCodeRules.NameKey(categoryName) == ThirdPartyCategoryName;
}
