namespace MobileBill.Domain.Enums;

// PdfSummaryPage: the "Total Due" printed on the bill's first (summary) page, read independently of the account rows.
public enum GrandTotalSource { None, PdfFooter, DerivedFromLines, PdfSummaryPage }
