using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.Notifications;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Reports;

// A Completed October 2026 batch: Factory A has an Executive (100) and a Staff (50) number, Factory B a Staff (80) number.
internal static class ReportDistributionTestData
{
    public static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static async Task<(Guid BatchId, User Hr, User Finance)> SeedAsync(MobileBillDbContext db, BillBatchStatus status = BillBatchStatus.Completed)
    {
        var provider = new TelecomProvider { Code = "DIALOG", Name = "Dialog Axiata" };
        var batch = new BillBatch { ProviderId = provider.Id, Provider = provider, CorporateCode = "CORP-1", BillingYear = 2026, BillingMonth = 10, Status = status, CalculatedGrandTotal = 230m, ValidationLevel = ValidationLevel.StructuralOnly };
        db.AddRange(provider, batch,
            new Factory { Code = "FA", Name = "Factory A" }, new Factory { Code = "FB", Name = "Factory B" },
            new EmployeeCategory { Code = "EX", Name = "Executive" }, new EmployeeCategory { Code = "ST", Name = "Staff" });
        AddBill(db, batch.Id, "0771000001", "FA", "Factory A", "EX", "Executive", 100m, Responsibility.ByUser, 20m);
        AddBill(db, batch.Id, "0771000002", "FA", "Factory A", "ST", "Staff", 50m, Responsibility.ByCompany, 0m);
        AddBill(db, batch.Id, "0771000003", "FB", "Factory B", "ST", "Staff", 80m, Responsibility.ByUser, 5m);
        var hr = new User { Email = "hr@concord.lk", DisplayName = "HR Manager", PasswordHash = "x", Role = UserRole.GroupHrManager, Status = UserAccountStatus.Active };
        var finance = new User { Email = "finance@concord.lk", DisplayName = "Finance User", PasswordHash = "x", Role = UserRole.FinanceUser, Status = UserAccountStatus.Active };
        db.Users.AddRange(hr, finance);
        await db.SaveChangesAsync();
        return (batch.Id, hr, finance);
    }

    private static void AddBill(MobileBillDbContext db, Guid batchId, string mobile, string factoryCode, string factory, string categoryCode, string category, decimal actualBill, Responsibility responsibility, decimal deduction)
    {
        var line = new BillLine { BillBatchId = batchId, MobileNumber = mobile, RawText = mobile, PageNumber = 1, ExtractionStatus = BillLineExtractionStatus.Extracted, TotalDueAmount = actualBill };
        db.Add(line);
        db.Add(new MonthlyBill
        {
            EmployeeId = Guid.NewGuid(), MobileAccountId = Guid.NewGuid(), BillLineId = line.Id, CreditLimit = 50m, MonthlyRental = 10m, ActualBill = actualBill,
            Variance = 60m - actualBill, CalculatedExcess = Math.Max(0, actualBill - 60m), FinalDeduction = deduction, Responsibility = responsibility,
            AssessedAt = DateTimeOffset.UnixEpoch, AssessedBy = "it", Status = MonthlyBillStatus.Approved,
            EmployeeEpfSnapshot = "E" + mobile[^1], EmployeeNameSnapshot = "Employee " + mobile[^1], CategoryCodeSnapshot = categoryCode, CategoryNameSnapshot = category,
            DesignationCodeSnapshot = "DES", DesignationNameSnapshot = "Designation", FactoryCodeSnapshot = factoryCode, FactoryNameSnapshot = factory,
            DepartmentCodeSnapshot = "DEP", DepartmentNameSnapshot = "Department", MobileNumberSnapshot = mobile, EntitlementEffectiveFromSnapshot = new DateOnly(2026, 1, 1),
        });
    }

    public sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 9, 4, 0, 0, TimeSpan.Zero);
    }

    public sealed class TestUser : ICurrentUserService
    {
        public string UserId => "it-user";
        public string DisplayName => "Pradeep Dissanayaka";
        public UserRole Role => UserRole.ITEngineer;
    }

    // Records what was sent; each attempt throws the next scripted failure, or succeeds when there is none.
    public sealed class RecordingSender(bool configured = true, params EmailDeliveryException?[] outcomes) : IEmailSender
    {
        private int attempt;
        public List<EmailMessage> Sent { get; } = [];
        public bool IsConfigured => configured;
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            var outcome = attempt < outcomes.Length ? outcomes[attempt] : null;
            attempt++;
            if (outcome is not null) return Task.FromException(outcome);
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
