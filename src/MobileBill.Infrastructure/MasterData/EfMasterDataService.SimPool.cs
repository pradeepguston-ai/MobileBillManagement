using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Calculations;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;

namespace MobileBill.Infrastructure.MasterData;

// SIM Pool: Assigned → Pooled (holder resigned) → Assigned to a new holder, or → Disconnected.
// Every change is written to the audit log.
public sealed partial class EfMasterDataService
{
    public async Task<MobileAccountDto> ReleaseToPoolAsync(Guid id, ReleaseToPoolRequest request, CancellationToken cancellationToken)
    {
        var account = await FindActiveAllocationAsync(id, cancellationToken);
        if (account.Status != SimStatus.Assigned) throw new MasterDataValidationException("Only an assigned mobile number can be released to the SIM Pool.");
        Pool(account, request.ResignedOn, request.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetMobileAccountAsync(id, cancellationToken);
    }

    public async Task<MobileAccountDto> AssignFromPoolAsync(Guid id, AssignFromPoolRequest request, CancellationToken cancellationToken)
    {
        RequireEmployeeSelected(request.EmployeeId);
        var pooled = await FindActiveAllocationAsync(id, cancellationToken);
        if (pooled.Status != SimStatus.Pooled) throw new MasterDataValidationException("Only a mobile number in the SIM Pool can be assigned from the pool.");
        await RequireActiveEmployeeAsync(request.EmployeeId, cancellationToken);
        var creditLimit = request.MonthlyCreditLimit ?? pooled.MonthlyCreditLimit;
        var rental = request.MonthlyRental ?? pooled.MonthlyRental;
        MasterDataValidation.RequireCurrencyAmount(creditLimit, "Monthly Credit Limit");
        MasterDataValidation.RequireCurrencyAmount(rental, "Monthly Rental");

        // The pool period stays on record as its own (now closed) allocation; the new holder starts a fresh one,
        // so earlier bills keep pointing at the allocation they were matched to.
        var now = Now;
        await using var transaction = _dbContext.Database.IsRelational() ? await _dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        pooled.IsActive = false;
        Touch(pooled, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var assigned = new MobileAccount { MobileNumber = pooled.MobileNumber, EmployeeId = request.EmployeeId, MonthlyCreditLimit = creditLimit, MonthlyRental = rental, CreatedAtUtc = now, CreatedBy = PerformedBy };
        _dbContext.MobileAccounts.Add(assigned);
        Audit(assigned.Id, "AssignedFromSimPool", new { pooled.MobileNumber, PooledAllocationId = pooled.Id }, new { request.EmployeeId, MonthlyCreditLimit = creditLimit, MonthlyRental = rental }, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetMobileAccountAsync(assigned.Id, cancellationToken);
    }

    public async Task<MobileAccountDto> DisconnectSimAsync(Guid id, DisconnectSimRequest request, CancellationToken cancellationToken)
    {
        var account = await FindActiveAllocationAsync(id, cancellationToken);
        var previous = new { Status = account.Status.ToString(), account.IsActive };
        var now = Now;
        account.Status = SimStatus.Disconnected;
        account.IsActive = false;
        account.DisconnectedOn = request.DisconnectedOn;
        account.StatusReason = TrimOptional(request.Reason) ?? account.StatusReason;
        Touch(account, now);
        Audit(account.Id, "SimDisconnected", previous, new { Status = account.Status.ToString(), account.DisconnectedOn, account.StatusReason }, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetMobileAccountAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<SimPoolItemDto>> GetSimPoolAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var rows = await _dbContext.MobileAccounts.AsNoTracking()
            .Where(account => account.IsActive && account.Status == SimStatus.Pooled)
            .OrderBy(account => account.PooledOn).ThenBy(account => account.MobileNumber)
            .Select(account => new { account.Id, account.MobileNumber, account.Employee.EPF, account.Employee.FullName, Factory = account.Employee.Factory.Name, Department = account.Employee.Department.Name, account.PooledOn, account.StatusReason, account.MonthlyCreditLimit, account.MonthlyRental })
            .ToListAsync(cancellationToken);
        return rows.Select(row =>
        {
            var pooledOn = row.PooledOn ?? today;
            return new SimPoolItemDto(row.Id, row.MobileNumber, row.EPF, row.FullName, row.Factory, row.Department, pooledOn, row.StatusReason,
                SimPoolRules.DaysInPool(pooledOn, today), SimPoolRules.IsLongIdle(pooledOn, today), row.MonthlyCreditLimit, row.MonthlyRental);
        }).ToList();
    }

    public async Task<IReadOnlyList<MobileAccountDto>> ResignEmployeeAsync(Guid employeeId, ResignEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees.FindAsync([employeeId], cancellationToken) ?? throw new MasterDataNotFoundException("Employee", employeeId);
        if (!employee.IsActive) throw new MasterDataValidationException("This employee is already inactive.");
        var numbers = await _dbContext.MobileAccounts
            .Where(account => account.EmployeeId == employee.Id && account.IsActive && account.Status == SimStatus.Assigned)
            .ToListAsync(cancellationToken);
        foreach (var account in numbers) Pool(account, request.ResignedOn, request.Reason ?? "Resigned");
        employee.IsActive = false;
        employee.UpdatedAtUtc = Now;
        employee.UpdatedBy = PerformedBy;
        Audit(employee.Id, "EmployeeResigned", new { employee.EPF }, new { request.ResignedOn, PooledNumbers = numbers.Select(account => account.MobileNumber).ToList() }, Now, nameof(Employee));
        await _dbContext.SaveChangesAsync(cancellationToken);
        var ids = numbers.Select(account => account.Id).ToList();
        return await MobileAccountProjection(_dbContext.MobileAccounts.AsNoTracking().Where(account => ids.Contains(account.Id)).OrderBy(account => account.MobileNumber)).ToListAsync(cancellationToken);
    }

    private void Pool(MobileAccount account, DateOnly resignedOn, string? reason)
    {
        var now = Now;
        account.Status = SimStatus.Pooled;
        account.PooledOn = resignedOn;
        account.StatusReason = TrimOptional(reason) ?? "Resigned";
        Touch(account, now);
        Audit(account.Id, "ReleasedToSimPool", new { Status = nameof(SimStatus.Assigned), account.EmployeeId }, new { Status = nameof(SimStatus.Pooled), account.PooledOn, account.StatusReason }, now);
    }

    private async Task<MobileAccount> FindActiveAllocationAsync(Guid id, CancellationToken token)
    {
        var account = await _dbContext.MobileAccounts.FindAsync([id], token) ?? throw new MasterDataNotFoundException("Mobile account", id);
        if (!account.IsActive) throw new MasterDataValidationException("This mobile allocation is no longer active.");
        return account;
    }

    private void Touch(MobileAccount account, DateTimeOffset now)
    {
        account.UpdatedAtUtc = now;
        account.UpdatedBy = PerformedBy;
    }

    private void Audit(Guid entityId, string action, object before, object after, DateTimeOffset now, string entityName = nameof(MobileAccount)) =>
        _dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = entityName, EntityId = entityId, Action = action,
            BeforeDataJson = JsonSerializer.Serialize(before), AfterDataJson = JsonSerializer.Serialize(after),
            PerformedBy = PerformedBy, PerformedAt = now, CreatedAtUtc = now, CreatedBy = PerformedBy
        });
}
