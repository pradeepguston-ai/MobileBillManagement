namespace MobileBill.Domain.Calculations;

public enum PooledBillPayer
{
    // The resigned employee's last month: billed and deducted as usual.
    Employee,
    // The SIM was already in the pool for this billing month: the company pays the whole bill.
    Company
}

// Who pays a pooled SIM's bill for a billing month. Batches are processed at the end of each month:
// if the holder resigned before the 10th of that month, the company pays; on or after the 10th,
// the month is still the employee's and the deduction is calculated as usual.
public static class SimPoolRules
{
    public const int CompanyPaysIfResignedBeforeDay = 10;

    // How long a SIM may sit in the pool before it is flagged as long idle.
    public const int LongIdleDays = 60;

    public static PooledBillPayer PayerFor(DateOnly resignedOn, int billingYear, int billingMonth)
    {
        var cutoff = new DateOnly(billingYear, billingMonth, CompanyPaysIfResignedBeforeDay);
        return resignedOn < cutoff ? PooledBillPayer.Company : PooledBillPayer.Employee;
    }

    public static int DaysInPool(DateOnly pooledOn, DateOnly today) => Math.Max(0, today.DayNumber - pooledOn.DayNumber);

    public static bool IsLongIdle(DateOnly pooledOn, DateOnly today) => DaysInPool(pooledOn, today) > LongIdleDays;
}
