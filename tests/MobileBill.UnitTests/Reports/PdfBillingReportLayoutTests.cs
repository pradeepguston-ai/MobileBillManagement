using MobileBill.Infrastructure.Reports;

namespace MobileBill.UnitTests.Reports;

public sealed class PdfBillingReportLayoutTests
{
    // 0-based PDF columns: 3 Name With Initials, 6 Designation, 7 Factory, 8 Department.
    [Theory]
    [InlineData("CONCORD MANUFACTURING (PVT) LTD", 7)]
    [InlineData("HR & ADMINISTRATION", 8)]
    [InlineData("QUALITY ASSURANCE (QA)", 8)]
    [InlineData("DOCUMENTATION ASSISTANT", 6)]
    public void Long_factory_department_and_designation_words_fit_at_the_normal_size(string text, int column) =>
        Assert.Null(PdfBillingReportService.FittingFontSize(text, column));

    [Fact]
    public void A_word_wider_than_its_column_gets_a_smaller_font_that_fits()
    {
        var size = PdfBillingReportService.FittingFontSize("MIHINDUKULASOORIYAMUNDAPPULAGE", 7);

        Assert.NotNull(size);
        Assert.InRange(size!.Value, 4.5, 6.4);
    }
}
