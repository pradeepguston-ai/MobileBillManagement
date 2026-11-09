using MobileBill.Application.Common;

namespace MobileBill.Application.Activity;

public sealed record ActivityLogEntryDto(Guid Id, DateTimeOffset OccurredAt, string UserId, string UserName, string Role, string Area, string Description);

// From and To are whole days (inclusive) in Sri Lanka time. Search matches the description or the user's name.
public sealed record ActivityLogRequest(string? UserId = null, string? Area = null, DateOnly? From = null, DateOnly? To = null, string? Search = null, int PageNumber = 1, int PageSize = 50);

public sealed record ActivityUserOptionDto(string UserId, string UserName);
public sealed record ActivityLogFilterOptionsDto(IReadOnlyList<ActivityUserOptionDto> Users, IReadOnlyList<string> Areas);

public sealed record ActivityRecord(string UserId, string UserName, string Role, string Area, string Description, string? TargetId, string HttpMethod, string Path);

// What a record is about, so its description can name it (a batch's billing period, a bill's mobile number, a user's name).
public enum ActivityTarget { BillBatch, MonthlyBill, BillException, User, PasswordResetRequest, MobileAccount, Employee, MobileDevice, ReportEmail, ReportRule, Factory, Department, Section, SubSection, Designation, Category, MobilePackage, ThirdPartyCompany, Provider }

public interface IActivityLogService
{
    Task RecordAsync(ActivityRecord record, CancellationToken cancellationToken);
    Task<PagedResult<ActivityLogEntryDto>> SearchAsync(ActivityLogRequest request, CancellationToken cancellationToken);
    Task<ActivityLogFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken);
    Task<string?> DescribeTargetAsync(ActivityTarget target, Guid id, CancellationToken cancellationToken);
}
