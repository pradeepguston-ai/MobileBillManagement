using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class MonthlyBillCalculationTests
{
    [Theory]
    [InlineData(80, 100, 20, 80, 120, 40, 0)]
    [InlineData(120, 100, 20, 120, 120, 0, 0)]
    [InlineData(175, 100, 20, 175, 120, -55, 55)]
    [InlineData(0, 100, 20, 0, 120, 120, 0)]
    [InlineData(50, 0, 20, 50, 20, -30, 30)]
    [InlineData(50, 20, 0, 50, 20, -30, 30)]
    [InlineData(50, 0, 0, 50, 0, -50, 50)]
    [InlineData(-15, 100, 20, -15, 120, 135, 0)]
    public void Create_calculates_expected_values(decimal totalDue, decimal creditLimit, decimal rental, decimal actual, decimal available, decimal variance, decimal excess)
    {
        var result = MonthlyBillCalculation.Create(totalDue, creditLimit, rental);

        Assert.Equal(actual, result.ActualBill);
        Assert.Equal(available, result.AvailableEntitlement);
        Assert.Equal(variance, result.Variance);
        Assert.Equal(excess, result.CalculatedExcess);
    }

    [Fact]
    public void Create_preserves_decimal_precision()
    {
        var result = MonthlyBillCalculation.Create(100.33m, 40.11m, 10.22m);

        Assert.Equal(50.33m, result.AvailableEntitlement);
        Assert.Equal(-50.00m, result.Variance);
        Assert.Equal(50.00m, result.CalculatedExcess);
    }
}
