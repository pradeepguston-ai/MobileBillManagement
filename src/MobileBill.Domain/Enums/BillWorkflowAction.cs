namespace MobileBill.Domain.Enums;

public enum BillWorkflowAction
{
    PrepareBatch,
    Submit,
    ApproveIt,
    ApproveHr,
    ApproveFinance,
    Reject,
    ReturnForCorrection,
    Lock
}
