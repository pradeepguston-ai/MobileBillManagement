namespace MobileBill.Domain.Enums;

// Persisted as strings to retain the business-facing exception codes.
public enum BillExceptionType
{
    MOBILE_NOT_FOUND,
    EMPLOYEE_NOT_ACTIVE,
    ENTITLEMENT_NOT_FOUND,
    MULTIPLE_ACTIVE_ALLOCATIONS,
    MULTIPLE_ENTITLEMENTS,
    ZERO_BILL,
    PARSER_WARNING,
    // A bill arrived for a SIM marked Disconnected; usually the provider is still charging for it.
    BILLED_AFTER_DISCONNECTION
}
