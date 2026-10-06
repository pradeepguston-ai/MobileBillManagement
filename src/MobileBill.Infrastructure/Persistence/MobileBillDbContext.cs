using Microsoft.EntityFrameworkCore;
using MobileBill.Domain.Entities;

namespace MobileBill.Infrastructure.Persistence;

public sealed class MobileBillDbContext(DbContextOptions<MobileBillDbContext> options)
    : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<MobileAccount> MobileAccounts => Set<MobileAccount>();

    public DbSet<Factory> Factories => Set<Factory>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<SubSection> SubSections => Set<SubSection>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<EmployeeCategory> EmployeeCategories => Set<EmployeeCategory>();
    public DbSet<TelecomProvider> TelecomProviders => Set<TelecomProvider>();
    public DbSet<DeductionRule> DeductionRules => Set<DeductionRule>();
    public DbSet<BillBatch> BillBatches => Set<BillBatch>();
    public DbSet<BillLine> BillLines => Set<BillLine>();
    public DbSet<MonthlyBill> MonthlyBills => Set<MonthlyBill>();
    public DbSet<BillException> BillExceptions => Set<BillException>();
    public DbSet<BillExceptionResolution> BillExceptionResolutions => Set<BillExceptionResolution>();
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MobileBillDbContext).Assembly);
    }
}
