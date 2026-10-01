namespace MobileBill.Domain.Enums;

public enum BillBatchStatus
{
    Draft,
    Uploaded,
    Parsed,
    ValidationFailed,
    Validated,
    ITReview,
    HRApproval,
    FinanceApproval,
    Completed,
    UnderReview,
    Approved,
    Locked
}
