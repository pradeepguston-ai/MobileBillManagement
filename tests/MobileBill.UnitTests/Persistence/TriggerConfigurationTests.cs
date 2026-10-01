using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MobileBill.Domain.Entities;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Persistence;

public sealed class TriggerConfigurationTests
{
    [Fact]
    public void Active_mobile_number_is_unique_and_monthly_amounts_use_decimal_precision()
    {
        var options = new DbContextOptionsBuilder<MobileBillDbContext>()
            .UseSqlServer("Server=.;Database=AllocationModelCheck;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new MobileBillDbContext(options);
        var account = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(MobileAccount))!;

        var numberIndex = Assert.Single(account.GetIndexes(), index =>
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(MobileAccount.MobileNumber));
        Assert.True(numberIndex.IsUnique);
        Assert.Equal("[IsActive] = 1", numberIndex.GetFilter());
        Assert.Equal(18, account.FindProperty(nameof(MobileAccount.MonthlyCreditLimit))!.GetPrecision());
        Assert.Equal(2, account.FindProperty(nameof(MobileAccount.MonthlyRental))!.GetScale());
    }
}
