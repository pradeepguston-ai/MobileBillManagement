using MobileBill.Application.Pdf;
using MobileBill.Infrastructure.Pdf;

namespace MobileBill.IntegrationTests.Pdf;

public sealed class SampleBillPdfParserTests
{
    [Fact]
    public async Task Sample_pdf_extracts_all_accounts_and_the_control_total()
    {
        await using var stream = File.OpenRead(FindSample("sample-bill.pdf"));

        var result = await new PdfBillParser().ParseAsync(stream, CancellationToken.None);

        var diagnostics = string.Join(Environment.NewLine,
        [
            $"Page counts: {string.Join(", ", result.Lines.GroupBy(line => line.PageNumber).Select(group => $"{group.Key}={group.Count()}"))}",
            $"Failed candidates: {string.Join(" | ", result.Lines.Where(line => line.ExtractionStatus == PdfBillLineExtractionStatus.Failed).Select(line => $"p{line.PageNumber} {line.MobileAccountNumber}: {line.ExtractionError}; {line.RawExtractedText}"))}",
            $"Duplicate accounts: {string.Join(" | ", result.Lines.GroupBy(line => line.MobileAccountNumber).Where(group => group.Count() > 1).Select(group => $"{group.Key} ({group.Count()})"))}"
        ]);

        Assert.True(result.TotalAccounts == 259, diagnostics);
        Assert.Equal(259, result.SuccessfulAccounts);
        Assert.Equal(0, result.FailedAccounts);
        var successfulLineTotalDue = result.Lines
            .Where(line => line.ExtractionStatus == PdfBillLineExtractionStatus.Success)
            .Sum(line => line.TotalDueAmount);

        Assert.Equal(390096.74m, successfulLineTotalDue);
        Assert.Equal(successfulLineTotalDue, result.GrandTotalDue);
        Assert.Equal(390096.74m, result.GrandTotalDue);
        Assert.Contains(result.Lines, line => line.MobileAccountNumber == "740052872" && line.PageNumber == 2);
        Assert.Contains(result.Lines, line => line.MobileAccountNumber == "761499198" && line.PageNumber is > 2 and < 9);
        Assert.Contains(result.Lines, line => line.MobileAccountNumber == "779442869" && line.PageNumber == 9);
        Assert.Contains(result.Lines, line => line.MobileAccountNumber == "768791861" && line.TotalDueAmount == 0m);
        Assert.Contains(result.Lines, line => line.IDD != 0m);
        Assert.Contains(result.Lines, line => line.Roaming != 0m);
        Assert.Contains(result.Lines, line => line.VAS != 0m);
        Assert.Contains(result.Lines, line => line.AddToBill != 0m);
        Assert.Contains(result.Lines, line => line.RawExtractedText.Contains(',', StringComparison.Ordinal) && line.TotalDueAmount > 1000m);
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
