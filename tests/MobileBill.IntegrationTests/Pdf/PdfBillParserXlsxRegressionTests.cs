using MobileBill.Application.Pdf;
using MobileBill.Infrastructure.Pdf;

namespace MobileBill.IntegrationTests.Pdf;

public sealed class PdfBillParserXlsxRegressionTests
{
    private static readonly (string Name, Func<ExpectedBillLine, decimal> Expected, Func<ParsedBillLine, decimal> Actual)[] MonetaryFields =
    [
        ("PreviousDueAmount", line => line.MonetaryValues[0], line => line.PreviousDueAmount),
        ("Payments", line => line.MonetaryValues[1], line => line.Payments),
        ("TotalUsageCharges", line => line.MonetaryValues[2], line => line.TotalUsageCharges),
        ("IDD", line => line.MonetaryValues[3], line => line.IDD),
        ("Roaming", line => line.MonetaryValues[4], line => line.Roaming),
        ("VAS", line => line.MonetaryValues[5], line => line.VAS),
        ("Discounts", line => line.MonetaryValues[6], line => line.Discounts),
        ("BillAdjustments", line => line.MonetaryValues[7], line => line.BillAdjustments),
        ("CommitmentCharges", line => line.MonetaryValues[8], line => line.CommitmentCharges),
        ("LatePaymentCharges", line => line.MonetaryValues[9], line => line.LatePaymentCharges),
        ("AddToBill", line => line.MonetaryValues[10], line => line.AddToBill),
        ("InstalmentPlans", line => line.MonetaryValues[11], line => line.InstalmentPlans),
        ("GovernmentTaxesLevies", line => line.MonetaryValues[12], line => line.GovernmentTaxesLevies),
        ("VAT", line => line.MonetaryValues[13], line => line.VAT),
        ("ChargesForBillPeriod", line => line.MonetaryValues[14], line => line.ChargesForBillPeriod),
        ("TotalDueAmount", line => line.MonetaryValues[15], line => line.TotalDueAmount)
    ];

    [Fact]
    public async Task Sample_pdf_matches_the_xlsx_account_level_reference_dataset()
    {
        var expected = SampleBillXlsxReferenceReader.Read(FindSample("sample-bill.xlsx"));
        await using var pdfStream = File.OpenRead(FindSample("sample-bill.pdf"));
        var parsed = await new PdfBillParser().ParseAsync(pdfStream, CancellationToken.None);
        var actualGroups = parsed.Lines
            .Where(line => line.ExtractionStatus == PdfBillLineExtractionStatus.Success)
            .GroupBy(line => line.MobileAccountNumber, StringComparer.Ordinal)
            .ToList();
        var duplicatePdfAccounts = actualGroups.Where(group => group.Count() > 1).Select(group => group.Key).OrderBy(account => account).ToList();
        var actual = actualGroups.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var missingAccounts = expected.Keys.Except(actual.Keys, StringComparer.Ordinal).OrderBy(account => account).ToList();
        var unexpectedAccounts = actual.Keys.Except(expected.Keys, StringComparer.Ordinal).OrderBy(account => account).ToList();
        var fieldMismatches = new List<string>();

        foreach (var account in expected.Keys.Intersect(actual.Keys, StringComparer.Ordinal).OrderBy(account => account))
        {
            foreach (var field in MonetaryFields)
            {
                var expectedValue = field.Expected(expected[account]);
                var actualValue = field.Actual(actual[account]);
                if (expectedValue != actualValue)
                {
                    fieldMismatches.Add($"Account {account}: {field.Name} expected {expectedValue:0.00}, actual {actualValue:0.00}");
                }
            }
        }

        var diagnostics = string.Join(Environment.NewLine,
        [
            $"Expected accounts: {expected.Count}; PDF successful accounts: {actual.Count}; PDF failed accounts: {parsed.FailedAccounts}.",
            $"Duplicate PDF accounts: {string.Join(", ", duplicatePdfAccounts)}",
            $"Missing accounts: {string.Join(", ", missingAccounts)}",
            $"Unexpected accounts: {string.Join(", ", unexpectedAccounts)}",
            $"Field mismatches:{Environment.NewLine}{string.Join(Environment.NewLine, fieldMismatches)}"
        ]);

        Assert.Equal(259, expected.Count);
        Assert.Equal(390096.74m, expected.Values.Sum(line => line.MonetaryValues[15]));
        Assert.True(duplicatePdfAccounts.Count == 0 && missingAccounts.Count == 0 && unexpectedAccounts.Count == 0 && fieldMismatches.Count == 0, diagnostics);
    }

    private static string FindSample(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "samples", fileName);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException($"Could not locate docs/samples/{fileName}.");
    }
}
