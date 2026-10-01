namespace MobileBill.Application.Pdf;

public interface IPdfBillParser
{
    Task<PdfBillParseResult> ParseAsync(Stream pdfStream, CancellationToken cancellationToken);
}
