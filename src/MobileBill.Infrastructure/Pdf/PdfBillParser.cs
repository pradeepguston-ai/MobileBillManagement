using System.Globalization;
using System.Text.RegularExpressions;
using MobileBill.Application.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MobileBill.Infrastructure.Pdf;

public sealed class PdfBillParser : IPdfBillParser
{
    private const double MobileAccountColumnRightBoundary = 120d;
    private static readonly Regex AccountPattern = new("^[0-9]{9,15}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MoneyPattern = new("^-?(?:\\d{1,3}(?:,\\d{3})*|\\d+)(?:\\.\\d{1,2})?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Task<PdfBillParseResult> ParseAsync(Stream pdfStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        return Task.Run(() => Parse(pdfStream, cancellationToken), cancellationToken);
    }

    private static PdfBillParseResult Parse(Stream pdfStream, CancellationToken cancellationToken)
    {
        var lines = new List<ParsedBillLine>();
        var warnings = new List<string>();
        decimal? grandTotalDue = null;

        using var document = PdfDocument.Open(pdfStream);
        var summaryTotalDue = document.NumberOfPages > 0 ? FindSummaryTotalDue(document.GetPage(1).GetWords().ToList()) : null;
        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.GetPage(pageNumber);
            var rows = BuildRows(page.GetWords());
            var pageText = string.Join(' ', rows.Select(row => row.Text));
            var hasAccountTable = pageText.Contains("MOBILE", StringComparison.OrdinalIgnoreCase)
                                  && pageText.Contains("ACCOUNT", StringComparison.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var accountIndex = FindAccountIndex(row.Words);
                if (hasAccountTable && accountIndex >= 0)
                {
                    var account = row.Words[accountIndex].Text;
                    var values = ExtractMonetaryValues(row.Words.Skip(accountIndex + 1));
                    if (values.Count == 16)
                    {
                        lines.Add(ParsedBillLine.Success(account, values, pageNumber, row.Text));
                    }
                    else
                    {
                        lines.Add(ParsedBillLine.Failed(account, pageNumber, row.Text, $"Expected 16 monetary values but found {values.Count}."));
                    }
                }
                else if (pageNumber == document.NumberOfPages)
                {
                    var values = ExtractMonetaryValues(row.Words);
                    // The final summary footer is a separate numeric row aligned to the
                    // sixteen monetary columns; it deliberately has no account number.
                    if (values.Count == 16)
                    {
                        grandTotalDue = values[^1];
                    }
                }
            }
        }

        if (grandTotalDue is null)
        {
            grandTotalDue = lines
                .Where(line => line.ExtractionStatus == PdfBillLineExtractionStatus.Success)
                .Sum(line => line.TotalDueAmount);
            if (summaryTotalDue is null)
                warnings.Add("PDF summary footer could not be extracted as text; Grand Total Due was calculated from successful account TotalDueAmount values.");
        }
        if (summaryTotalDue is null)
            warnings.Add("The Total Due on the first page of the PDF could not be read, so the account total cannot be checked against it.");

        return new PdfBillParseResult(lines, grandTotalDue.Value, warnings, summaryTotalDue,
            summaryTotalDue is null ? MobileBill.Domain.Enums.GrandTotalSource.DerivedFromLines : MobileBill.Domain.Enums.GrandTotalSource.PdfSummaryPage);
    }

    // The first page carries the invoice summary, ending in a "Total Due" line whose amount sits on the same
    // printed line (a point or two lower or higher than the label). Reading it gives a total that does not
    // depend on the account rows, so the parsed rows can be checked against it.
    internal static decimal? FindSummaryTotalDue(IReadOnlyList<Word> words)
    {
        const double sameLineTolerance = 3d;
        for (var index = 0; index < words.Count; index++)
        {
            if (!string.Equals(words[index].Text, "Total", StringComparison.OrdinalIgnoreCase)) continue;
            var total = words[index].BoundingBox;
            var nextOnLine = words
                .Where(word => word.BoundingBox.Left > total.Left && Math.Abs(word.BoundingBox.Bottom - total.Bottom) <= sameLineTolerance)
                .OrderBy(word => word.BoundingBox.Left)
                .ToList();
            if (nextOnLine.Count == 0 || !string.Equals(nextOnLine[0].Text, "Due", StringComparison.OrdinalIgnoreCase)) continue;
            // "Total Due Amount" style labels are allowed; any other text between the label and the amount is not.
            var amount = nextOnLine.Skip(1)
                .SkipWhile(word => string.Equals(word.Text, "Amount", StringComparison.OrdinalIgnoreCase) || word.Text is "Rs." or "LKR" or ":")
                .FirstOrDefault();
            if (amount is null || !MoneyPattern.IsMatch(amount.Text)) continue;
            return decimal.Parse(amount.Text, NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }
        return null;
    }

    private static IReadOnlyList<Row> BuildRows(IEnumerable<Word> words)
    {
        return words
            .GroupBy(word => Math.Round(word.BoundingBox.Bottom * 2d) / 2d)
            .OrderByDescending(group => group.Key)
            .Select(group => new Row(
                group.OrderBy(word => word.BoundingBox.Left)
                    .Select(word => new PositionedWord(word.Text, word.BoundingBox.Left))
                    .ToList()))
            .ToList();
    }

    private static List<decimal> ExtractMonetaryValues(IEnumerable<PositionedWord> words)
    {
        return words
            .Select(word => word.Text)
            .Where(token => MoneyPattern.IsMatch(token))
            .Select(token => decimal.Parse(token, NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture))
            .ToList();
    }

    private static int FindAccountIndex(IReadOnlyList<PositionedWord> words)
    {
        for (var index = 0; index < words.Count; index++)
        {
            if (words[index].Left <= MobileAccountColumnRightBoundary && AccountPattern.IsMatch(words[index].Text)) return index;
        }

        return -1;
    }

    private sealed record PositionedWord(string Text, double Left);

    private sealed record Row(IReadOnlyList<PositionedWord> Words)
    {
        public string Text => string.Join(" ", Words.Select(word => word.Text));
    }
}
