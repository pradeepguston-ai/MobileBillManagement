using System.Text.RegularExpressions;

namespace MobileBill.Application.Pdf;

public sealed record ParsedBillLine(
    string MobileAccountNumber,
    decimal PreviousDueAmount,
    decimal Payments,
    decimal TotalUsageCharges,
    decimal IDD,
    decimal Roaming,
    decimal VAS,
    decimal Discounts,
    decimal BillAdjustments,
    decimal CommitmentCharges,
    decimal LatePaymentCharges,
    decimal AddToBill,
    decimal InstalmentPlans,
    decimal GovernmentTaxesLevies,
    decimal VAT,
    decimal ChargesForBillPeriod,
    decimal TotalDueAmount,
    int PageNumber,
    string RawExtractedText,
    PdfBillLineExtractionStatus ExtractionStatus,
    string? ExtractionError)
{
    private static readonly Regex AccountNumberPattern = new("^[0-9]{9,15}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ParsedBillLine Success(string mobileAccountNumber, IReadOnlyList<decimal> values, int pageNumber, string rawExtractedText)
    {
        if (!AccountNumberPattern.IsMatch(mobileAccountNumber)) throw new ArgumentException("Mobile account number must contain 9 to 15 digits.", nameof(mobileAccountNumber));
        if (values.Count != 16) throw new ArgumentException("A bill line must contain exactly 16 monetary values.", nameof(values));
        return new ParsedBillLine(mobileAccountNumber, values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15], pageNumber, rawExtractedText, PdfBillLineExtractionStatus.Success, null);
    }

    public static ParsedBillLine Failed(string mobileAccountNumber, int pageNumber, string rawExtractedText, string extractionError) =>
        new(mobileAccountNumber, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, pageNumber, rawExtractedText, PdfBillLineExtractionStatus.Failed, extractionError);
}
