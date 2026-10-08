using System.Text.RegularExpressions;

namespace MobileBill.Application.Assistant;

public enum PersonalDataKind { Person, Epf, Mobile }

// Replaces employee names, EPF numbers and mobile numbers with placeholders (PERSON-1, EPF-1, MOBILE-1) before text
// leaves the system for the AI model, and puts the real values back in the model's answer. One masker lives for
// one conversation, so the same person keeps the same placeholder in every message of it.
public sealed partial class PersonalDataMasker
{
    private readonly Dictionary<(PersonalDataKind Kind, string Key), string> tokens = [];
    private readonly Dictionary<string, string> originals = new(StringComparer.Ordinal);
    private readonly Dictionary<PersonalDataKind, int> counters = [];

    public string Mask(PersonalDataKind kind, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value ?? string.Empty;
        var original = value.Trim();
        var key = (kind, Key(kind, original));
        if (tokens.TryGetValue(key, out var token)) return token;

        var next = counters.GetValueOrDefault(kind) + 1;
        counters[kind] = next;
        token = $"{Prefix(kind)}-{next}";
        tokens[key] = token;
        originals[token] = original;
        return token;
    }

    public string Person(string? name) => Mask(PersonalDataKind.Person, name);
    public string Epf(string? epf) => Mask(PersonalDataKind.Epf, epf);
    public string Mobile(string? mobileNumber) => Mask(PersonalDataKind.Mobile, mobileNumber);

    // Masks every occurrence in free text of the given values (found in it by the caller) and of values already
    // masked in this conversation. Longer values go first, so a full name is replaced before a calling name inside it.
    public string MaskText(string text, IEnumerable<(PersonalDataKind Kind, string Value)> found)
    {
        foreach (var (kind, value) in found) Mask(kind, value);
        foreach (var (token, original) in originals.OrderByDescending(pair => pair.Value.Length))
        {
            var kind = KindOf(token);
            var pattern = kind == PersonalDataKind.Mobile ? MobilePattern(original) : Regex.Escape(original);
            // Not inside a word, a longer number or a placeholder such as EPF-12.
            text = Regex.Replace(text, $@"(?<![\p{{L}}\d-]){pattern}(?![\p{{L}}\d])", token, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return text;
    }

    // Puts the real values back; placeholders this conversation never issued are left as they are.
    public string Unmask(string? text) => string.IsNullOrEmpty(text)
        ? text ?? string.Empty
        : TokenRegex().Replace(text, match => originals.GetValueOrDefault(match.Value) ?? match.Value);

    // The digits a mobile number is stored with: no spaces, no leading 0 or 94 country code.
    public static string NormalizeMobile(string value)
    {
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith("94", StringComparison.Ordinal)) return digits[2..];
        if (digits.Length == 10 && digits.StartsWith('0')) return digits[1..];
        return digits;
    }

    private static string Key(PersonalDataKind kind, string value) => kind switch
    {
        PersonalDataKind.Mobile => NormalizeMobile(value),
        PersonalDataKind.Person => WhiteSpaceRegex().Replace(value, " ").ToUpperInvariant(),
        _ => value.ToUpperInvariant(),
    };

    // A mobile number also matches when written with a leading 0 or +94.
    private static string MobilePattern(string original) => $@"(?:\+?94|0)?{Regex.Escape(NormalizeMobile(original))}";

    private static string Prefix(PersonalDataKind kind) => kind switch { PersonalDataKind.Person => "PERSON", PersonalDataKind.Epf => "EPF", _ => "MOBILE" };

    private static PersonalDataKind KindOf(string token) => token.StartsWith("PERSON", StringComparison.Ordinal) ? PersonalDataKind.Person
        : token.StartsWith("EPF", StringComparison.Ordinal) ? PersonalDataKind.Epf : PersonalDataKind.Mobile;

    [GeneratedRegex(@"\b(?:PERSON|EPF|MOBILE)-\d+\b")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}
