using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Entities;

namespace MobileBill.Infrastructure.MasterData;

// Mobile packages: a provider's plans. Every change is written to the audit log.
public sealed partial class EfMasterDataService
{
    public async Task<PagedResult<MobilePackageDto>> GetPackagesAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var query = ApplyStatusFilter(_dbContext.MobilePackages.AsNoTracking(), request.IsActive, package => package.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim(); query = query.Where(package => package.Code.Contains(search) || package.Description.Contains(search)); }
        return await ToPagedResultAsync(PackageProjection(query.OrderBy(package => package.Code).ThenBy(package => package.Provider.Name)), request, cancellationToken);
    }

    public async Task<MobilePackageDto> GetPackageAsync(Guid id, CancellationToken cancellationToken) =>
        await PackageProjection(_dbContext.MobilePackages.AsNoTracking().Where(package => package.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw new MasterDataNotFoundException("Mobile package", id);

    public async Task<MobilePackageDto> CreatePackageAsync(MobilePackageUpsertRequest request, CancellationToken cancellationToken)
    {
        await ValidatePackageAsync(request, null, cancellationToken);
        var package = new MobilePackage
        {
            Code = request.Code.Trim(), ProviderId = request.ProviderId, Description = request.Description.Trim(),
            MonthlyRental = request.MonthlyRental, TotalWithTax = request.TotalWithTax, DefaultCreditLimit = request.DefaultCreditLimit,
            CreatedAtUtc = Now, CreatedBy = PerformedBy
        };
        _dbContext.MobilePackages.Add(package);
        Audit(package.Id, "MobilePackageCreated", new { }, PackageAuditData(package), Now, nameof(MobilePackage));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetPackageAsync(package.Id, cancellationToken);
    }

    // Changing a package does not change its allocations; use ApplyPackageRentalAsync for that.
    public async Task<MobilePackageDto> UpdatePackageAsync(Guid id, MobilePackageUpsertRequest request, CancellationToken cancellationToken)
    {
        var package = await _dbContext.MobilePackages.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Mobile package", id);
        await ValidatePackageAsync(request, id, cancellationToken);
        var before = PackageAuditData(package);
        package.Code = request.Code.Trim(); package.ProviderId = request.ProviderId; package.Description = request.Description.Trim();
        package.MonthlyRental = request.MonthlyRental; package.TotalWithTax = request.TotalWithTax; package.DefaultCreditLimit = request.DefaultCreditLimit;
        package.UpdatedAtUtc = Now; package.UpdatedBy = PerformedBy;
        Audit(package.Id, "MobilePackageUpdated", before, PackageAuditData(package), Now, nameof(MobilePackage));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetPackageAsync(id, cancellationToken);
    }

    // An inactive package cannot be chosen for new allocations; allocations already on it keep it.
    public async Task DeactivatePackageAsync(Guid id, CancellationToken cancellationToken)
    {
        var package = await _dbContext.MobilePackages.FindAsync([id], cancellationToken) ?? throw new MasterDataNotFoundException("Mobile package", id);
        package.IsActive = false; package.UpdatedAtUtc = Now; package.UpdatedBy = PerformedBy;
        Audit(package.Id, "MobilePackageDeactivated", new { package.Code }, new { package.IsActive }, Now, nameof(MobilePackage));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ApplyPackageRentalAsync(Guid id, CancellationToken cancellationToken)
    {
        var package = await _dbContext.MobilePackages.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw new MasterDataNotFoundException("Mobile package", id);
        var allocations = await _dbContext.MobileAccounts.Where(account => account.IsActive && account.PackageId == id && account.MonthlyRental != package.MonthlyRental).ToListAsync(cancellationToken);
        var now = Now;
        foreach (var account in allocations)
        {
            Audit(account.Id, "PackageRentalApplied", new { account.MonthlyRental }, new { package.Code, package.MonthlyRental }, now);
            account.MonthlyRental = package.MonthlyRental;
            Touch(account, now);
        }
        if (allocations.Count > 0) await _dbContext.SaveChangesAsync(cancellationToken);
        return allocations.Count;
    }

    // A new allocation must have an active package; an existing one keeps any package it has and may not drop it.
    private async Task<Guid?> ResolveAllocationPackageAsync(Guid? requested, Guid? current, CancellationToken token)
    {
        if (requested is null || requested == Guid.Empty)
        {
            if (current is not null) throw new MasterDataValidationException("Select the package. A package cannot be removed from an allocation.");
            return null;
        }
        if (requested == current) return requested;
        if (!await _dbContext.MobilePackages.AnyAsync(package => package.Id == requested && package.IsActive, token))
            throw new MasterDataValidationException("Select an active mobile package.");
        return requested;
    }

    private async Task ValidatePackageAsync(MobilePackageUpsertRequest request, Guid? id, CancellationToken token)
    {
        MasterDataValidation.RequireText(request.Code, "Package Code");
        MasterDataValidation.RequireText(request.Description, "Package Description");
        if (request.Code.Trim().Length > 50) throw new MasterDataValidationException("Package Code must be 50 characters or fewer.");
        if (request.Description.Trim().Length > 500) throw new MasterDataValidationException("Package Description must be 500 characters or fewer.");
        MasterDataValidation.RequireCurrencyAmount(request.MonthlyRental, "Rental");
        MasterDataValidation.RequireCurrencyAmount(request.TotalWithTax, "Total with Tax");
        MasterDataValidation.RequireCurrencyAmount(request.DefaultCreditLimit, "Default Credit Limit");
        if (request.TotalWithTax < request.MonthlyRental) throw new MasterDataValidationException("Total with Tax cannot be less than the Rental.");
        if (!await _dbContext.TelecomProviders.AnyAsync(provider => provider.Id == request.ProviderId && provider.IsActive, token))
            throw new MasterDataValidationException("Select an active telecom provider.");
        var code = request.Code.Trim();
        if (await _dbContext.MobilePackages.AnyAsync(package => package.ProviderId == request.ProviderId && package.Code == code && package.Id != id, token))
            throw new MasterDataConflictException("This package code already exists for the selected provider.");
    }

    private static object PackageAuditData(MobilePackage package) =>
        new { package.Code, package.ProviderId, package.Description, package.MonthlyRental, package.TotalWithTax, package.DefaultCreditLimit };

    private static IQueryable<MobilePackageDto> PackageProjection(IQueryable<MobilePackage> query) => query.Select(package => new MobilePackageDto(
        package.Id, package.Code, package.ProviderId, package.Provider.Name, package.Description, package.MonthlyRental, package.TotalWithTax, package.DefaultCreditLimit, package.IsActive,
        package.MobileAccounts.Count(account => account.IsActive),
        package.MobileAccounts.Count(account => account.IsActive && account.MonthlyRental != package.MonthlyRental)));
}
