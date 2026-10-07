using MobileBill.Domain.Entities;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Application;

// A provider and an active package for tests that create allocations, which need a package.
internal static class TestPackages
{
    public static readonly Guid PackageId = Guid.Parse("7a1e2b3c-0000-4000-8000-000000000700");

    public static void Add(MobileBillDbContext context)
    {
        var provider = new TelecomProvider { Code = "PRV", Name = "Provider" };
        context.AddRange(provider, new MobilePackage { Id = PackageId, Code = "PPU23_700", ProviderId = provider.Id, Description = "Test package", MonthlyRental = 100m, TotalWithTax = 130m, DefaultCreditLimit = 1000m });
    }
}
