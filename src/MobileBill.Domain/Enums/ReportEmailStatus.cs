namespace MobileBill.Domain.Enums;

public enum ReportEmailStatus
{
    // Waiting to be sent, or to be tried again after a failure the mail service may recover from.
    Pending,
    Sent,
    // Given up: the report could not be built, it was too large, or the mail service kept refusing it. Can be resent.
    Failed
}
