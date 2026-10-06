using MobileBill.Domain.Calculations;

namespace MobileBill.UnitTests.Domain;

public sealed class SimPoolRulesTests
{
    [Theory]
    [InlineData("2026-09-01", PooledBillPayer.Company)]
    [InlineData("2026-09-09", PooledBillPayer.Company)]
    [InlineData("2026-09-10", PooledBillPayer.Employee)]
    [InlineData("2026-09-25", PooledBillPayer.Employee)]
    [InlineData("2026-08-20", PooledBillPayer.Company)]   // resigned after the 10th of August: August was the employee's, September is the company's
    [InlineData("2026-10-02", PooledBillPayer.Employee)]  // a resignation after the billing month does not move that month to the company
    public void Company_pays_only_when_the_holder_resigned_before_the_10th_of_the_billing_month(string resignedOn, PooledBillPayer expected)
    {
        Assert.Equal(expected, SimPoolRules.PayerFor(DateOnly.Parse(resignedOn), 2026, 9));
    }

    [Theory]
    [InlineData("2026-08-01", "2026-09-30", 60, false)]
    [InlineData("2026-08-01", "2026-10-01", 61, true)]
    [InlineData("2026-10-05", "2026-10-01", 0, false)]
    public void Days_in_pool_and_long_idle_flag(string pooledOn, string today, int expectedDays, bool expectedLongIdle)
    {
        var pooled = DateOnly.Parse(pooledOn); var now = DateOnly.Parse(today);
        Assert.Equal(expectedDays, SimPoolRules.DaysInPool(pooled, now));
        Assert.Equal(expectedLongIdle, SimPoolRules.IsLongIdle(pooled, now));
    }
}
