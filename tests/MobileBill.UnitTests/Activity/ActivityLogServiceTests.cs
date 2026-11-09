using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Activity;
using MobileBill.Application.Common;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Activity;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Activity;

public sealed class ActivityLogServiceTests
{
    [Fact]
    public async Task Lists_activity_newest_first_and_filters_by_user_area_day_and_text()
    {
        await using var db = CreateDb();
        var clock = new TestClock();
        var service = new EfActivityLogService(db, clock);
        // 2 Nov 23:00 UTC is 3 Nov 04:30 in Sri Lanka.
        await Record(service, clock, new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero), "u1", "Pradeep", "Billing", "Uploaded the October 2026 bill PDF");
        await Record(service, clock, new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.Zero), "u2", "Kumara", "Approvals", "Approved the October 2026 bill batch (HR approval)");
        await Record(service, clock, new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero), "u1", "Pradeep Dissanayaka", "Sign-in", "Signed in");

        var all = await service.SearchAsync(new ActivityLogRequest(), default);
        Assert.Equal(["Signed in", "Approved the October 2026 bill batch (HR approval)", "Uploaded the October 2026 bill PDF"], all.Items.Select(x => x.Description));
        Assert.Equal(("u1", "Pradeep Dissanayaka", "ITEngineer"), (all.Items[0].UserId, all.Items[0].UserName, all.Items[0].Role));

        Assert.Equal(2, (await service.SearchAsync(new ActivityLogRequest(UserId: "u1"), default)).TotalCount);
        Assert.Equal(["Approvals"], (await service.SearchAsync(new ActivityLogRequest(Area: "Approvals"), default)).Items.Select(x => x.Area));
        Assert.Equal(["Approved the October 2026 bill batch (HR approval)"], (await service.SearchAsync(new ActivityLogRequest(From: new DateOnly(2026, 11, 3), To: new DateOnly(2026, 11, 3)), default)).Items.Select(x => x.Description));
        Assert.Equal(2, (await service.SearchAsync(new ActivityLogRequest(Search: "October"), default)).TotalCount);
        Assert.Equal(1, (await service.SearchAsync(new ActivityLogRequest(Search: "Kumara"), default)).TotalCount);
        var page = await service.SearchAsync(new ActivityLogRequest(PageNumber: 2, PageSize: 2), default);
        Assert.Equal((3, 1), (page.TotalCount, page.Items.Count));
    }

    [Fact]
    public async Task Filter_options_list_each_user_under_their_latest_name_and_every_area()
    {
        await using var db = CreateDb();
        var clock = new TestClock();
        var service = new EfActivityLogService(db, clock);
        await Record(service, clock, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), "u1", "Pradeep", "Billing", "a");
        await Record(service, clock, new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero), "u1", "Pradeep Dissanayaka", "Sign-in", "b");
        await Record(service, clock, new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero), "u2", "Kumara", "Approvals", "c");

        var options = await service.GetFilterOptionsAsync(default);

        Assert.Equal([("u2", "Kumara"), ("u1", "Pradeep Dissanayaka")], options.Users.Select(x => (x.UserId, x.UserName)));
        Assert.Equal(["Approvals", "Billing", "Sign-in"], options.Areas);
    }

    [Fact]
    public async Task Long_values_are_cut_to_fit()
    {
        await using var db = CreateDb();
        var service = new EfActivityLogService(db, new TestClock());

        await service.RecordAsync(new ActivityRecord("u1", "User", "Viewer", "Billing", new string('x', 800), null, "POST", "/api"), default);

        Assert.Equal(500, (await db.ActivityLogs.SingleAsync()).Description.Length);
    }

    [Fact]
    public async Task Describes_targets_by_their_names()
    {
        await using var db = CreateDb();
        var provider = new TelecomProvider { Code = "DIALOG", Name = "Dialog Axiata" };
        var batch = new BillBatch { ProviderId = provider.Id, Provider = provider, CorporateCode = "C", BillingYear = 2026, BillingMonth = 10 };
        var factory = new Factory { Code = "CAPL", Name = "Concord Apparel" };
        var user = new User { Email = "hr@concord.lk", DisplayName = "Kumara Mohottige", PasswordHash = "x" };
        db.AddRange(provider, batch, factory, user);
        await db.SaveChangesAsync();
        var service = new EfActivityLogService(db, new TestClock());

        Assert.Equal("October 2026", await service.DescribeTargetAsync(ActivityTarget.BillBatch, batch.Id, default));
        Assert.Equal("Concord Apparel (CAPL)", await service.DescribeTargetAsync(ActivityTarget.Factory, factory.Id, default));
        Assert.Equal("Dialog Axiata (DIALOG)", await service.DescribeTargetAsync(ActivityTarget.Provider, provider.Id, default));
        Assert.Equal("Kumara Mohottige", await service.DescribeTargetAsync(ActivityTarget.User, user.Id, default));
        Assert.Null(await service.DescribeTargetAsync(ActivityTarget.Employee, Guid.NewGuid(), default));
    }

    private static async Task Record(EfActivityLogService service, TestClock clock, DateTimeOffset at, string userId, string name, string area, string description)
    {
        clock.UtcNow = at;
        await service.RecordAsync(new ActivityRecord(userId, name, nameof(UserRole.ITEngineer), area, description, null, "POST", "/api/x"), default);
    }

    private static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } }
}
