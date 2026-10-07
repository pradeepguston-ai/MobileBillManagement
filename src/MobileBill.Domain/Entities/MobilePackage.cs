using MobileBill.Domain.Common;

namespace MobileBill.Domain.Entities;

// A provider's mobile plan, for example PPU23_700. Choosing it for an allocation fills in the rental and a suggested
// credit limit; the allocation keeps its own amounts, which are what billing uses.
public sealed class MobilePackage : AuditableEntity
{
    public required string Code { get; set; }
    public Guid ProviderId { get; set; }
    public required string Description { get; set; }
    // Before tax.
    public decimal MonthlyRental { get; set; }
    // As shown on the provider's plan; for reference only (the entitlement uses the rental).
    public decimal TotalWithTax { get; set; }
    public decimal DefaultCreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public TelecomProvider Provider { get; set; } = null!;
    public ICollection<MobileAccount> MobileAccounts { get; } = new List<MobileAccount>();
}
