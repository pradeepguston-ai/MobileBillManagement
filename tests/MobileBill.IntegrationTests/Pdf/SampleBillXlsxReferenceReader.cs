using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace MobileBill.IntegrationTests.Pdf;

internal sealed record ExpectedBillLine(string MobileAccountNumber, IReadOnlyList<decimal> MonetaryValues);

internal static class SampleBillXlsxReferenceReader
{
    private static readonly Regex AccountNumberPattern = new("^[0-9]{9,15}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<string, ExpectedBillLine> Read(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First(worksheet => worksheet.LastRowUsed() is not null);
        var result = new Dictionary<string, ExpectedBillLine>(StringComparer.Ordinal);

        // The supplied converted workbook has four presentation rows followed by a
        // multi-row header, and its account data begins at row 8 in columns A:Q.
        for (var rowNumber = 8; rowNumber <= worksheet.LastRowUsed()!.RowNumber(); rowNumber++)
        {
            var account = worksheet.Cell(rowNumber, 1).GetFormattedString().Trim();
            if (!AccountNumberPattern.IsMatch(account)) continue;

            var values = Enumerable.Range(2, 16)
                .Select(columnNumber => ParseMoney(worksheet.Cell(rowNumber, columnNumber).GetFormattedString(), rowNumber, columnNumber))
                .ToArray();

            if (!result.TryAdd(account, new ExpectedBillLine(account, values)))
            {
                throw new InvalidDataException($"The XLSX reference contains duplicate mobile account {account}.");
            }
        }

        return result;
    }

    private static decimal ParseMoney(string value, int rowNumber, int columnNumber)
    {
        if (decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidDataException($"XLSX reference cell R{rowNumber}C{columnNumber} is not a valid monetary value: '{value}'.");
    }
}
