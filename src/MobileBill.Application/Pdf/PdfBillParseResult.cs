namespace MobileBill.Application.Pdf;

public sealed record PdfBillParseResult(IReadOnlyList<ParsedBillLine> Lines, decimal GrandTotalDue, IReadOnlyList<string> Warnings, decimal? StatedGrandTotal = null, MobileBill.Domain.Enums.GrandTotalSource GrandTotalSource = MobileBill.Domain.Enums.GrandTotalSource.None)
{
    public int TotalAccounts => Lines.Count;
    public int SuccessfulAccounts => Lines.Count(line => line.ExtractionStatus == PdfBillLineExtractionStatus.Success);
    public int FailedAccounts => Lines.Count(line => line.ExtractionStatus == PdfBillLineExtractionStatus.Failed);
}
