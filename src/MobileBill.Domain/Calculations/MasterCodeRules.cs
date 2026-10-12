using System.Globalization;

namespace MobileBill.Domain.Calculations;

// Codes for Departments, Designations, Categories, Sections, Sub Sections, Telecom Providers and Third Party Companies are generated, not typed: the prefix
// and a running number (DEP001, DEP002 …). People only see and use the names; the code never changes, so a
// master can be renamed without touching the employees that use it.
public static class MasterCodeRules
{
    public const string Department = "DEP";
    public const string Designation = "DES";
    public const string Category = "CAT";
    public const string Section = "SEC";
    public const string SubSection = "SUB";
    public const string Provider = "PRV";
    public const string ThirdPartyCompany = "TPC";

    // At least three digits; past 999 the number simply grows (DEP1000).
    public const int MinimumDigits = 3;

    // The next free code after the highest running number already used with this prefix. Codes in another format
    // (typed by hand before codes were generated) are ignored.
    public static string NextCode(string prefix, IEnumerable<string> existingCodes)
    {
        var highest = 0;
        foreach (var code in existingCodes)
        {
            if (code.Length <= prefix.Length || !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var digits = code[prefix.Length..];
            if (digits.All(char.IsAsciiDigit) && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > highest) highest = number;
        }
        return prefix + (highest + 1).ToString(new string('0', MinimumDigits), CultureInfo.InvariantCulture);
    }

    // Names are compared ignoring case and repeated spaces, so "Human  Resources" and "human resources" are the same.
    public static string NameKey(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();
}
