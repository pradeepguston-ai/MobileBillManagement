using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Activity;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Activity;

public sealed class EfActivityLogService(MobileBillDbContext db, IClock clock) : IActivityLogService
{
    // Day filters are Sri Lanka days (no daylight saving).
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromHours(5.5);

    public async Task RecordAsync(ActivityRecord record, CancellationToken cancellationToken)
    {
        db.ActivityLogs.Add(new ActivityLog
        {
            OccurredAt = clock.UtcNow, UserId = Cut(record.UserId, 256), UserName = Cut(record.UserName, 256), Role = Cut(record.Role, 50),
            Area = Cut(record.Area, 100), Description = Cut(record.Description, 500), TargetId = record.TargetId is null ? null : Cut(record.TargetId, 100),
            HttpMethod = Cut(record.HttpMethod, 10), Path = Cut(record.Path, 500),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<ActivityLogEntryDto>> SearchAsync(ActivityLogRequest request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var pageNumber = Math.Max(1, request.PageNumber);
        var query = db.ActivityLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.UserId)) query = query.Where(item => item.UserId == request.UserId);
        if (!string.IsNullOrWhiteSpace(request.Area)) query = query.Where(item => item.Area == request.Area);
        if (request.From is { } from) { var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), SriLankaOffset); query = query.Where(item => item.OccurredAt >= start); }
        if (request.To is { } to) { var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), SriLankaOffset); query = query.Where(item => item.OccurredAt < end); }
        if (request.Search?.Trim() is { Length: > 0 } search) query = query.Where(item => item.Description.Contains(search) || item.UserName.Contains(search));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.OccurredAt)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(item => new ActivityLogEntryDto(item.Id, item.OccurredAt, item.UserId, item.UserName, item.Role, item.Area, item.Description))
            .ToListAsync(cancellationToken);
        return new PagedResult<ActivityLogEntryDto>(items, pageNumber, pageSize, total);
    }

    public async Task<ActivityLogFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        // Each user under their latest name.
        var users = (await db.ActivityLogs.AsNoTracking()
                .GroupBy(item => item.UserId)
                .Select(group => group.OrderByDescending(item => item.OccurredAt).Select(item => new { item.UserId, item.UserName }).First())
                .ToListAsync(cancellationToken))
            .OrderBy(user => user.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(user => new ActivityUserOptionDto(user.UserId, user.UserName)).ToList();
        var areas = await db.ActivityLogs.AsNoTracking().Select(item => item.Area).Distinct().OrderBy(area => area).ToListAsync(cancellationToken);
        return new ActivityLogFilterOptionsDto(users, areas);
    }

    public async Task<string?> DescribeTargetAsync(ActivityTarget target, Guid id, CancellationToken cancellationToken) => target switch
    {
        ActivityTarget.BillBatch => await db.BillBatches.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new { item.BillingYear, item.BillingMonth }).SingleOrDefaultAsync(cancellationToken) is { } batch ? Period(batch.BillingYear, batch.BillingMonth) : null,
        ActivityTarget.MonthlyBill => await db.MonthlyBills.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new { item.MobileNumberSnapshot, item.BillLine.BillBatch.BillingYear, item.BillLine.BillBatch.BillingMonth }).SingleOrDefaultAsync(cancellationToken) is { } bill
            ? $"{bill.MobileNumberSnapshot} ({Period(bill.BillingYear, bill.BillingMonth)})" : null,
        ActivityTarget.BillException => await db.BillExceptions.AsNoTracking().Where(item => item.Id == id)
            .Select(item => item.BillLine != null ? item.BillLine.MobileNumber : null).SingleOrDefaultAsync(cancellationToken),
        ActivityTarget.User => await db.Users.AsNoTracking().Where(item => item.Id == id).Select(item => item.DisplayName).SingleOrDefaultAsync(cancellationToken),
        ActivityTarget.PasswordResetRequest => await db.PasswordResetRequests.AsNoTracking().Where(item => item.Id == id).Select(item => item.User.DisplayName).SingleOrDefaultAsync(cancellationToken),
        ActivityTarget.MobileAccount => await db.MobileAccounts.AsNoTracking().Where(item => item.Id == id).Select(item => item.MobileNumber).SingleOrDefaultAsync(cancellationToken),
        ActivityTarget.Employee => await db.Employees.AsNoTracking().Where(item => item.Id == id).Select(item => item.EPF + " " + item.FullName).SingleOrDefaultAsync(cancellationToken),
        ActivityTarget.MobileDevice => await db.MobileDevices.AsNoTracking().Where(item => item.Id == id).Select(item => item.AssetTag).SingleOrDefaultAsync(cancellationToken),
        ActivityTarget.ReportEmail => await db.ReportEmailDeliveries.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new { item.RuleName, item.BillBatch.BillingYear, item.BillBatch.BillingMonth }).SingleOrDefaultAsync(cancellationToken) is { } email
            ? $"{Period(email.BillingYear, email.BillingMonth)} report to {email.RuleName}" : null,
        ActivityTarget.ReportRule => await db.ReportDistributionRules.AsNoTracking().Where(item => item.Id == id).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken),
        ActivityTarget.Factory => await NameAndCode(db.Factories.Where(item => item.Id == id).Select(item => new { item.Name, item.Code }), cancellationToken),
        ActivityTarget.Department => await NameAndCode(db.Departments.Where(item => item.Id == id).Select(item => new { item.Name, item.Code }), cancellationToken),
        ActivityTarget.Section => await NameAndCode(db.Sections.Where(item => item.Id == id).Select(item => new { item.Name, item.Code }), cancellationToken),
        ActivityTarget.SubSection => await NameAndCode(db.SubSections.Where(item => item.Id == id).Select(item => new { item.Name, item.Code }), cancellationToken),
        ActivityTarget.Designation => await NameAndCode(db.Designations.Where(item => item.Id == id).Select(item => new { item.Name, item.Code }), cancellationToken),
        ActivityTarget.Category => await NameAndCode(db.EmployeeCategories.Where(item => item.Id == id).Select(item => new { item.Name, item.Code }), cancellationToken),
        ActivityTarget.MobilePackage => await NameAndCode(db.MobilePackages.Where(item => item.Id == id).Select(item => new { Name = item.Description, item.Code }), cancellationToken),
        ActivityTarget.ThirdPartyCompany => await NameAndCode(db.ThirdPartyCompanies.Where(item => item.Id == id).Select(item => new { item.Name, item.Code }), cancellationToken),
        ActivityTarget.Provider => await NameAndCode(db.TelecomProviders.Where(item => item.Id == id).Select(item => new { item.Name, item.Code }), cancellationToken),
        _ => null,
    };

    private static async Task<string?> NameAndCode<T>(IQueryable<T> query, CancellationToken cancellationToken) where T : class =>
        await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken) is { } item
            ? $"{item.GetType().GetProperty("Name")!.GetValue(item)} ({item.GetType().GetProperty("Code")!.GetValue(item)})"
            : null;

    private static string Period(int year, int month) => $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)} {year}";
    private static string Cut(string value, int length) => value.Length <= length ? value : value[..length];
}
