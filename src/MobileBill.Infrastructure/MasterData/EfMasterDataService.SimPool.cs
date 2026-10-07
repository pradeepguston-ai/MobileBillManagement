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
        var packageId = request.PackageId is null ? pooled.PackageId : await ResolveAllocationPackageAsync(request.PackageId, pooled.PackageId, cancellationToken);
        MasterDataValidation.RequireCurrencyAmount(creditLimit, "Monthly Credit Limit");
        MasterDataValidation.RequireCurrencyAmount(rental, "Monthly Rental");

        // The pool period stays on record as its own (now closed) allocation; the new holder starts a fresh one,
        // so earlier bills keep pointing at the allocation they were matched to.
        var now = Now;
        await using var transaction = _dbContext.Database.IsRelational() ? await _dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        pooled.IsActive = false;
        Touch(pooled, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var assigned = new MobileAccount { MobileNumber = pooled.MobileNumber, EmployeeId = request.EmployeeId, MonthlyCreditLimit = creditLimit, MonthlyRental = rental, PackageId = packageId, SimType = pooled.SimType, CreatedAtUtc = now, CreatedBy = PerformedBy };
        _dbContext.MobileAccounts.Add(assigned);
        Audit(assigned.Id, "AssignedFromSimPool", new { pooled.MobileNumber, PooledAllocationId = pooled.Id }, new { request.EmployeeId, MonthlyCreditLimit = creditLimit, MonthlyRental = rental, PackageId = packageId }, now);
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

    // A future date only records the resignation (pending); the employee keeps working and keeps their numbers
    // until that day. Resigning a pending employee again changes the date or reason, or completes it now.
    public async Task<IReadOnlyList<MobileAccountDto>> ResignEmployeeAsync(Guid employeeId, ResignEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees.FindAsync([employeeId], cancellationToken) ?? throw new MasterDataNotFoundException("Employee", employeeId);
        if (!employee.IsActive) throw new MasterDataValidationException("This employee is already inactive.");
        var reason = TrimOptional(request.Reason) ?? "Resigned";
        if (reason.Length > 250) throw new MasterDataValidationException("Reason must be 250 characters or fewer.");
        if (ResignationRules.IsPending(request.ResignedOn, Today))
        {
            var before = new { employee.ResignedOn, employee.ResignationReason };
            employee.ResignedOn = request.ResignedOn;
            employee.ResignationReason = reason;
            TouchEmployee(employee);
            Audit(employee.Id, "EmployeeResignationScheduled", before, new { employee.EPF, request.ResignedOn, Reason = reason }, Now, nameof(Employee));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return [];
        }
        var pooled = await CompleteResignationAsync(employee, request.ResignedOn, reason, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var ids = pooled.Select(account => account.Id).ToList();
        return await MobileAccountProjection(_dbContext.MobileAccounts.AsNoTracking().Where(account => ids.Contains(account.Id)).OrderBy(account => account.MobileNumber)).ToListAsync(cancellationToken);
    }

    public async Task CancelResignationAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees.FindAsync([employeeId], cancellationToken) ?? throw new MasterDataNotFoundException("Employee", employeeId);
        if (!employee.IsActive || employee.ResignedOn is not { } resignedOn || !ResignationRules.IsPending(resignedOn, Today))
            throw new MasterDataValidationException("Only a pending resignation can be cancelled.");
        Audit(employee.Id, "EmployeeResignationCancelled", new { employee.ResignedOn, employee.ResignationReason }, new { employee.EPF }, Now, nameof(Employee));
        employee.ResignedOn = null;
        employee.ResignationReason = null;
        TouchEmployee(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CompleteDueResignationsAsync(CancellationToken cancellationToken)
    {
        var today = Today;
        var due = await _dbContext.Employees.Where(employee => employee.IsActive && employee.ResignedOn != null && employee.ResignedOn <= today).ToListAsync(cancellationToken);
        if (due.Count == 0) return 0;
        _performedByOverride = SystemUser;
        try
        {
            foreach (var employee in due) await CompleteResignationAsync(employee, employee.ResignedOn!.Value, employee.ResignationReason ?? "Resigned", cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally { _performedByOverride = null; }
        return due.Count;
    }

    public async Task<IReadOnlyList<EmployeeResignationDto>> GetResignationsAsync(ResignationStatus status, CancellationToken cancellationToken)
    {
        // Pending resignations whose date has arrived are completed first, so they show as Resigned.
        await CompleteDueResignationsAsync(cancellationToken);
        var today = Today;
        var pending = status == ResignationStatus.Pending;
        var employees = await _dbContext.Employees.AsNoTracking()
            .Where(employee => employee.ResignedOn != null && employee.IsActive == pending)
            .Select(employee => new
            {
                employee.Id, employee.EPF, employee.FullName, employee.CallingName, Factory = employee.Factory.Name, Department = employee.Department.Name,
                Section = employee.Section != null ? employee.Section.Name : null, ResignedOn = employee.ResignedOn!.Value, employee.ResignationReason,
                Numbers = employee.MobileAccounts.Where(account => account.IsActive && account.Status == (pending ? SimStatus.Assigned : SimStatus.Pooled))
                    .OrderBy(account => account.MobileNumber).Select(account => account.MobileNumber).ToList(),
                // Pending: devices they hold. Resigned: devices still to be collected from them.
                Devices = _dbContext.MobileDevices.Where(device => device.CurrentEmployeeId == employee.Id && device.Status == (pending ? DeviceStatus.Issued : DeviceStatus.ReturnPending))
                    .OrderBy(device => device.AssetTag).Select(device => device.AssetTag + " (" + device.Brand + " " + device.Model + ")").ToList()
            })
            .ToListAsync(cancellationToken);
        var rows = employees.Select(row => new EmployeeResignationDto(row.Id, row.EPF, row.FullName, row.CallingName, row.Factory, row.Department, row.Section,
            row.ResignedOn, row.ResignationReason, ResignationRules.DaysLeft(row.ResignedOn, today), row.Numbers, row.Devices));
        // Pending: the soonest leaver first. Resigned: the most recent first.
        return (pending ? rows.OrderBy(row => row.ResignedOn) : rows.OrderByDescending(row => row.ResignedOn)).ThenBy(row => row.Epf, StringComparer.Ordinal).ToList();
    }

    // Releases the employee's numbers to the SIM Pool (dated by the resignation, which decides who pays that
    // month's bill) and deactivates them. The caller saves.
    private async Task<List<MobileAccount>> CompleteResignationAsync(Employee employee, DateOnly resignedOn, string reason, CancellationToken cancellationToken)
    {
        var numbers = await _dbContext.MobileAccounts
            .Where(account => account.EmployeeId == employee.Id && account.IsActive && account.Status == SimStatus.Assigned)
            .ToListAsync(cancellationToken);
        foreach (var account in numbers) Pool(account, resignedOn, reason);
        // Company devices have to be collected by hand, so they stay with the leaver as Return Pending until returned.
        var devices = await _dbContext.MobileDevices.Where(device => device.CurrentEmployeeId == employee.Id && device.Status == DeviceStatus.Issued).ToListAsync(cancellationToken);
        foreach (var device in devices)
        {
            device.Status = DeviceStatus.ReturnPending;
            device.StatusSince = resignedOn;
            device.UpdatedAtUtc = Now;
            device.UpdatedBy = PerformedBy;
            Audit(device.Id, "DeviceReturnPending", new { Status = nameof(DeviceStatus.Issued) }, new { Status = nameof(DeviceStatus.ReturnPending), EmployeeId = employee.Id, ResignedOn = resignedOn }, Now, nameof(MobileDevice));
        }
        employee.IsActive = false;
        employee.ResignedOn = resignedOn;
        employee.ResignationReason = reason;
        TouchEmployee(employee);
        Audit(employee.Id, "EmployeeResigned", new { employee.EPF }, new { ResignedOn = resignedOn, Reason = reason, PooledNumbers = numbers.Select(account => account.MobileNumber).ToList() }, Now, nameof(Employee));
        return numbers;
    }

    private DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);

    private void TouchEmployee(Employee employee)
    {
        employee.UpdatedAtUtc = Now;
        employee.UpdatedBy = PerformedBy;
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
