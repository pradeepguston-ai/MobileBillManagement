namespace MobileBill.Domain.Enums;

// The state of a mobile allocation (SIM). Persisted as a string.
public enum SimStatus
{
    // In use by the employee on the allocation.
    Assigned,
    // Released by a resigned employee and waiting for a new holder; still billed, paid by the company.
    Pooled,
    // Given up with the provider; any later bill is flagged.
    Disconnected
}
