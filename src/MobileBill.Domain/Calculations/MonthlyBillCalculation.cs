namespace MobileBill.Domain.Calculations;

public sealed record MonthlyBillCalculation(
    decimal ActualBill,
    decimal AvailableEntitlement,
    decimal Variance,
    decimal CalculatedExcess)
{
    public static MonthlyBillCalculation Create(decimal totalDueAmount, decimal monthlyCreditLimit, decimal monthlyRental)
    {
        var availableEntitlement = monthlyCreditLimit + monthlyRental;
        var variance = availableEntitlement - totalDueAmount;
        var calculatedExcess = Math.Max(0m, totalDueAmount - availableEntitlement);
        return new MonthlyBillCalculation(totalDueAmount, availableEntitlement, variance, calculatedExcess);
    }
}
