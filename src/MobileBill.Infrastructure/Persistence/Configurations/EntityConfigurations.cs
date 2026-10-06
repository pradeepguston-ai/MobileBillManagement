using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MobileBill.Domain.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;

namespace MobileBill.Infrastructure.Persistence.Configurations;

internal abstract class AuditableEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : AuditableEntity
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(entity => entity.CreatedBy).HasMaxLength(256);
        builder.Property(entity => entity.UpdatedBy).HasMaxLength(256);
        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}

internal sealed class FactoryConfiguration : AuditableEntityConfiguration<Factory>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Factory> builder)
    {
        builder.ToTable("Factories");
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.HasAlternateKey(entity => entity.Code);
    }
}

internal sealed class DepartmentConfiguration : AuditableEntityConfiguration<Department>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.HasAlternateKey(entity => entity.Code);
    }
}

internal sealed class SectionConfiguration : AuditableEntityConfiguration<Section>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections");
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.DepartmentCode).HasMaxLength(50).IsRequired();
        builder.HasAlternateKey(entity => entity.Code);
        builder.HasOne(entity => entity.Department).WithMany(department => department.Sections).HasForeignKey(entity => entity.DepartmentCode).HasPrincipalKey(department => department.Code).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubSectionConfiguration : AuditableEntityConfiguration<SubSection>
{
    protected override void ConfigureEntity(EntityTypeBuilder<SubSection> builder)
    {
        builder.ToTable("SubSections");
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.SectionCode).HasMaxLength(50).IsRequired();
        builder.HasAlternateKey(entity => entity.Code);
        builder.HasOne(entity => entity.Section).WithMany(section => section.SubSections).HasForeignKey(entity => entity.SectionCode).HasPrincipalKey(section => section.Code).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DesignationConfiguration : AuditableEntityConfiguration<Designation>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Designation> builder)
    {
        builder.ToTable("Designations");
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.HasAlternateKey(entity => entity.Code);
    }
}

internal sealed class EmployeeCategoryConfiguration : AuditableEntityConfiguration<EmployeeCategory>
{
    protected override void ConfigureEntity(EntityTypeBuilder<EmployeeCategory> builder)
    {
        builder.ToTable("EmployeeCategories");
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.HasAlternateKey(entity => entity.Code);
    }
}

internal sealed class TelecomProviderConfiguration : AuditableEntityConfiguration<TelecomProvider>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TelecomProvider> builder)
    {
        builder.ToTable("TelecomProviders");
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique();
    }
}

internal sealed class DeductionRuleConfiguration : AuditableEntityConfiguration<DeductionRule>
{
    protected override void ConfigureEntity(EntityTypeBuilder<DeductionRule> builder)
    {
        builder.ToTable("DeductionRules");
        builder.Property(entity => entity.Code).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(2000);
        builder.HasIndex(entity => entity.Code).IsUnique();
    }
}

internal sealed class EmployeeConfiguration : AuditableEntityConfiguration<Employee>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.Property(entity => entity.EPF).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.FullName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CallingName).HasMaxLength(100);
        builder.Property(entity => entity.CategoryCode).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.DesignationCode).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.FactoryCode).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.DepartmentCode).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.SectionCode).HasMaxLength(50);
        builder.Property(entity => entity.SubSectionCode).HasMaxLength(50);
        builder.Property(entity => entity.DefaultResponsibility).HasConversion<string>().HasMaxLength(50);
        // The same EPF number can be used in different factories, so EPF is unique only together with the factory.
        builder.HasIndex(entity => new { entity.FactoryCode, entity.EPF }).IsUnique();
        builder.HasIndex(entity => entity.EPF);
        builder.HasIndex(entity => entity.IsActive);
        builder.HasOne(entity => entity.Category).WithMany(category => category.Employees).HasForeignKey(entity => entity.CategoryCode).HasPrincipalKey(category => category.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Designation).WithMany(designation => designation.Employees).HasForeignKey(entity => entity.DesignationCode).HasPrincipalKey(designation => designation.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Factory).WithMany(factory => factory.Employees).HasForeignKey(entity => entity.FactoryCode).HasPrincipalKey(factory => factory.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Department).WithMany(department => department.Employees).HasForeignKey(entity => entity.DepartmentCode).HasPrincipalKey(department => department.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Section).WithMany(section => section.Employees).HasForeignKey(entity => entity.SectionCode).HasPrincipalKey(section => section.Code).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.SubSection).WithMany(subSection => subSection.Employees).HasForeignKey(entity => entity.SubSectionCode).HasPrincipalKey(subSection => subSection.Code).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MobileAccountConfiguration : AuditableEntityConfiguration<MobileAccount>
{
    protected override void ConfigureEntity(EntityTypeBuilder<MobileAccount> builder)
    {
        builder.ToTable("MobileAccounts", table =>
        {
            table.HasCheckConstraint("CK_MobileAccounts_MonthlyCreditLimit", "[MonthlyCreditLimit] >= 0");
            table.HasCheckConstraint("CK_MobileAccounts_MonthlyRental", "[MonthlyRental] >= 0");
        });
        builder.Property(entity => entity.MobileNumber).HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.MonthlyCreditLimit).HasPrecision(18, 2);
        builder.Property(entity => entity.MonthlyRental).HasPrecision(18, 2);
        builder.HasIndex(entity => entity.MobileNumber).IsUnique().HasFilter("[IsActive] = 1");
        builder.HasIndex(entity => entity.EmployeeId);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(50).HasDefaultValue(SimStatus.Assigned).HasSentinel(SimStatus.Assigned);
        builder.Property(entity => entity.PooledOn).HasColumnType("date");
        builder.Property(entity => entity.StatusReason).HasMaxLength(500);
        builder.Property(entity => entity.DisconnectedOn).HasColumnType("date");
        builder.HasIndex(entity => entity.Status);
        builder.HasOne(entity => entity.Employee).WithMany(employee => employee.MobileAccounts)
            .HasForeignKey(entity => entity.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class BillBatchConfiguration : AuditableEntityConfiguration<BillBatch>
{
    protected override void ConfigureEntity(EntityTypeBuilder<BillBatch> builder)
    {
        builder.ToTable("BillBatches", table => { table.HasCheckConstraint("CK_BillBatches_BillingMonth", "[BillingMonth] BETWEEN 1 AND 12"); table.HasCheckConstraint("CK_BillBatches_BillingYear", "[BillingYear] >= 2000"); });
        builder.Property(entity => entity.CorporateCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.OriginalFileName).HasMaxLength(260);
        builder.Property(entity => entity.StoredFilePath).HasMaxLength(500);
        builder.Property(entity => entity.FileHash).HasMaxLength(128);
        builder.Property(entity => entity.StatedGrandTotal).HasPrecision(18, 2);
        builder.Property(entity => entity.CalculatedGrandTotal).HasPrecision(18, 2);
        builder.Property(entity => entity.Difference).HasPrecision(18, 2);
        builder.Property(entity => entity.GrandTotalSource).HasConversion<string>().HasMaxLength(50).HasDefaultValue(GrandTotalSource.None);
        builder.Property(entity => entity.ValidationLevel).HasConversion<string>().HasMaxLength(50).HasDefaultValue(ValidationLevel.None);
        builder.Property(entity => entity.ValidationWarning).HasMaxLength(2000);
        builder.Property(entity => entity.ValidatedBy).HasMaxLength(256);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.UploadedBy).HasMaxLength(256);
        builder.HasIndex(entity => new { entity.ProviderId, entity.BillingYear, entity.BillingMonth });
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.FileHash).IsUnique().HasFilter("[FileHash] IS NOT NULL");
        builder.HasOne(entity => entity.Provider).WithMany(provider => provider.BillBatches).HasForeignKey(entity => entity.ProviderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillLineConfiguration : AuditableEntityConfiguration<BillLine>
{
    protected override void ConfigureEntity(EntityTypeBuilder<BillLine> builder)
    {
        builder.ToTable("BillLines");
        builder.Property(entity => entity.MobileNumber).HasMaxLength(30).IsRequired();
        builder.Property(entity => entity.RawText).IsRequired();
        builder.Property(entity => entity.ExtractionStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.ExtractionError).HasMaxLength(2000);
        foreach (var property in new[] { nameof(BillLine.PreviousDueAmount), nameof(BillLine.Payments), nameof(BillLine.TotalUsageCharges), nameof(BillLine.Idd), nameof(BillLine.Roaming), nameof(BillLine.ValueAddedServices), nameof(BillLine.Discounts), nameof(BillLine.BillAdjustmentsBalanceTransfers), nameof(BillLine.CommitmentCharges), nameof(BillLine.LatePaymentCharges), nameof(BillLine.AddToBill), nameof(BillLine.InstalmentPlans), nameof(BillLine.GovernmentTaxesAndLevies), nameof(BillLine.Vat), nameof(BillLine.ChargesForBillPeriod), nameof(BillLine.TotalDueAmount) }) builder.Property(property).HasPrecision(18, 2);
        builder.HasIndex(entity => new { entity.BillBatchId, entity.MobileNumber });
        builder.HasIndex(entity => entity.ExtractionStatus);
        builder.HasOne(entity => entity.BillBatch).WithMany(batch => batch.BillLines).HasForeignKey(entity => entity.BillBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MonthlyBillConfiguration : AuditableEntityConfiguration<MonthlyBill>
{
    protected override void ConfigureEntity(EntityTypeBuilder<MonthlyBill> builder)
    {
        builder.ToTable("MonthlyBills");
        foreach (var property in new[] { nameof(MonthlyBill.CreditLimit), nameof(MonthlyBill.MonthlyRental), nameof(MonthlyBill.ActualBill), nameof(MonthlyBill.Variance), nameof(MonthlyBill.CalculatedExcess), nameof(MonthlyBill.FinalDeduction), nameof(MonthlyBill.DeductionOverrideAmount) }) builder.Property(property).HasPrecision(18, 2);
        builder.Property(entity => entity.Responsibility).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.AssessedBy).HasMaxLength(256);
        builder.Property(entity => entity.DeductionOverrideReason).HasMaxLength(2000);
        builder.Property(entity => entity.DeductionOverrideBy).HasMaxLength(256);
        builder.Property(entity => entity.Remark).HasMaxLength(2000);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.EmployeeEpfSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.EmployeeNameSnapshot).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.CallingNameSnapshot).HasMaxLength(100);
        builder.Property(entity => entity.CategoryCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.CategoryNameSnapshot).HasMaxLength(200);
        builder.Property(entity => entity.DesignationCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.DesignationNameSnapshot).HasMaxLength(200);
        builder.Property(entity => entity.FactoryCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.FactoryNameSnapshot).HasMaxLength(200);
        builder.Property(entity => entity.DepartmentCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.DepartmentNameSnapshot).HasMaxLength(200);
        builder.Property(entity => entity.SectionCodeSnapshot).HasMaxLength(50);
        builder.Property(entity => entity.SectionNameSnapshot).HasMaxLength(200);
        builder.Property(entity => entity.SubSectionCodeSnapshot).HasMaxLength(50);
        builder.Property(entity => entity.SubSectionNameSnapshot).HasMaxLength(200);
        builder.Property(entity => entity.MobileNumberSnapshot).HasMaxLength(30).IsRequired();        builder.Property(entity => entity.EntitlementEffectiveFromSnapshot).HasColumnType("date");
        builder.Property(entity => entity.EntitlementEffectiveToSnapshot).HasColumnType("date");
        builder.Property(entity => entity.AllocationMatchMethod).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.EntitlementMatchMethod).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(entity => entity.EmployeeId);
        builder.HasIndex(entity => entity.MobileAccountId);
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.BillLineId).IsUnique();
        builder.HasOne(entity => entity.Employee).WithMany(employee => employee.MonthlyBills).HasForeignKey(entity => entity.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.MobileAccount).WithMany(account => account.MonthlyBills).HasForeignKey(entity => entity.MobileAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.BillLine).WithOne(line => line.MonthlyBill).HasForeignKey<MonthlyBill>(entity => entity.BillLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillExceptionConfiguration : AuditableEntityConfiguration<BillException>
{
    protected override void ConfigureEntity(EntityTypeBuilder<BillException> builder)
    {
        builder.ToTable("BillExceptions");
        builder.Property(entity => entity.ExceptionType).HasConversion<string>().HasMaxLength(100);
        builder.Property(entity => entity.Severity).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.Description).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.Resolution).HasMaxLength(4000);
        builder.Property(entity => entity.ResolvedBy).HasMaxLength(256);
        builder.HasIndex(entity => new { entity.BillBatchId, entity.Status });
        builder.HasIndex(entity => entity.BillLineId);
        builder.HasIndex(entity => entity.MonthlyBillId);
        builder.HasOne(entity => entity.BillBatch).WithMany(batch => batch.Exceptions).HasForeignKey(entity => entity.BillBatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.BillLine).WithMany(line => line.Exceptions).HasForeignKey(entity => entity.BillLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.MonthlyBill).WithMany(bill => bill.Exceptions).HasForeignKey(entity => entity.MonthlyBillId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillExceptionResolutionConfiguration : AuditableEntityConfiguration<BillExceptionResolution>
{
    protected override void ConfigureEntity(EntityTypeBuilder<BillExceptionResolution> builder)
    {
        builder.ToTable("BillExceptionResolutions");
        builder.Property(x => x.MobileNumber).HasMaxLength(30).IsRequired(); builder.Property(x => x.EmployeeEpf).HasMaxLength(50).IsRequired(); builder.Property(x => x.EmployeeName).HasMaxLength(256).IsRequired(); builder.Property(x => x.ResolutionComment).HasMaxLength(2000).IsRequired(); builder.Property(x => x.ResolvedBy).HasMaxLength(256).IsRequired(); builder.Property(x => x.OriginalExceptionType).HasConversion<string>().HasMaxLength(100);
        builder.HasIndex(x => x.BillExceptionId);
        builder.HasOne(x => x.BillException).WithMany(x => x.Resolutions).HasForeignKey(x => x.BillExceptionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApprovalHistoryConfiguration : AuditableEntityConfiguration<ApprovalHistory>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ApprovalHistory> builder)
    {
        builder.ToTable("ApprovalHistories");
        builder.Property(entity => entity.Stage).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.WorkflowRole).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.Decision).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.Comment).HasMaxLength(4000);
        builder.Property(entity => entity.UserId).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.PreviousStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.NewStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.ApprovedBy).HasMaxLength(256).IsRequired();
        builder.HasIndex(entity => new { entity.BillBatchId, entity.Stage });
        builder.HasOne(entity => entity.BillBatch).WithMany(batch => batch.ApprovalHistory).HasForeignKey(entity => entity.BillBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserConfiguration : AuditableEntityConfiguration<User>
{
    protected override void ConfigureEntity(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.Property(entity => entity.Email).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(entity => entity.Email).IsUnique();
        builder.HasIndex(entity => entity.Status);
    }
}

internal sealed class PasswordResetRequestConfiguration : AuditableEntityConfiguration<PasswordResetRequest>
{
    protected override void ConfigureEntity(EntityTypeBuilder<PasswordResetRequest> builder)
    {
        builder.ToTable("PasswordResetRequests");
        builder.Property(entity => entity.NewPasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(entity => entity.DecidedBy).HasMaxLength(256);
        builder.HasIndex(entity => new { entity.UserId, entity.Status });
        builder.HasIndex(entity => entity.Status);
        builder.HasOne(entity => entity.User).WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditLogConfiguration : AuditableEntityConfiguration<AuditLog>
{
    protected override void ConfigureEntity(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.Property(entity => entity.EntityName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Action).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BeforeDataJson).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.AfterDataJson).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.PerformedBy).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.CorrelationId).HasMaxLength(100);
        builder.HasIndex(entity => new { entity.EntityName, entity.EntityId, entity.PerformedAt });
    }
}
