using MobileBill.Application.Pdf;

namespace MobileBill.UnitTests.Pdf;

public sealed class PdfBillParserContractTests
{
    [Fact]
    public void Parsed_bill_line_preserves_a_leading_zero_account_and_decimal_values()
    {
        var values = Enumerable.Repeat(0m, 16).ToArray();
        values[0] = 1300.00m;
        values[15] = 30728.06m;

        var line = ParsedBillLine.Success("0761499198", values, 2, "0761499198 1,300.00 ... 30,728.06");

        Assert.Equal("0761499198", line.MobileAccountNumber);
        Assert.Equal(1300.00m, line.PreviousDueAmount);
        Assert.Equal(30728.06m, line.TotalDueAmount);
        Assert.Equal(PdfBillLineExtractionStatus.Success, line.ExtractionStatus);
    }

    [Fact]
    public void Failed_candidate_retains_page_raw_text_and_reason()
    {
        var line = ParsedBillLine.Failed("0761499198", 4, "0761499198 malformed", "Expected 16 monetary values but found 3.");

        Assert.Equal(PdfBillLineExtractionStatus.Failed, line.ExtractionStatus);
        Assert.Equal(4, line.PageNumber);
        Assert.NotNull(line.ExtractionError);
    }
}
