using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Reports;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;
using MobileBill.Infrastructure.Reports;
using static MobileBill.UnitTests.Reports.ReportDistributionTestData;

namespace MobileBill.UnitTests.Reports;

public sealed class ReportDistributionServiceTests
{
    [Fact]
    public async Task A_rule_holds_several_system_users_and_typed_addresses_without_duplicates()
    {
        await using var db = CreateDb();
        var (_, hr, finance) = await SeedAsync(db);

        var rule = await Service(db).CreateRuleAsync(new SaveReportDistributionRuleRequest(" Factory A - Executives and Staff ", ["FA"], ["EX", "ST"],
            [hr.Id, finance.Id], ["manager.fa@concord.lk", " ", "HR@concord.lk", "Manager.FA@concord.lk"]), default);

        Assert.Equal(("Factory A - Executives and Staff", "Factory A", "Executive, Staff", true), (rule.Name, rule.FactoryLabel, rule.CategoryLabel, rule.IsActive));
        // Users first by name, then typed addresses; the typed copy of the HR user's address and the repeated address are dropped.
        Assert.Equal(["Finance User", "HR Manager", null], rule.Recipients.Select(x => x.DisplayName));
        Assert.Equal(["finance@concord.lk", "hr@concord.lk", "manager.fa@concord.lk"], rule.Recipients.Select(x => x.Email));
    }

    [Fact]
    public async Task No_factories_or_categories_means_all_of_them()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var rule = await Service(db).CreateRuleAsync(new SaveReportDistributionRuleRequest("Everything", null, [], null, ["cfo@concord.lk"]), default);

        Assert.Equal(("All factories", "All categories"), (rule.FactoryLabel, rule.CategoryLabel));
    }

    [Theory]
    [InlineData("", "FA", "EX", "a@concord.lk", "rule name")]
    [InlineData("Rule", "XX", "EX", "a@concord.lk", "Factory 'XX' was not found")]
    [InlineData("Rule", "FA", "XX", "a@concord.lk", "Category 'XX' was not found")]
    [InlineData("Rule", "FA", "EX", "not-an-email", "'not-an-email' is not a valid email address")]
    [InlineData("Rule", "FA", "EX", "", "Add at least one recipient")]
    [InlineData("Existing", "FA", "EX", "a@concord.lk", "already exists")]
    public async Task A_rule_needs_a_unique_name_known_factories_and_categories_and_valid_recipients(string name, string factory, string category, string email, string error)
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        await Service(db).CreateRuleAsync(new SaveReportDistributionRuleRequest("Existing", [], [], null, ["x@concord.lk"]), default);

        var refusal = await Assert.ThrowsAsync<ReportDistributionValidationException>(() =>
            Service(db).CreateRuleAsync(new SaveReportDistributionRuleRequest(name, [factory], [category], null, [email]), default));

        Assert.Contains(error, refusal.Message);
    }

    [Fact]
    public async Task An_inactive_user_cannot_be_added_but_one_already_on_the_rule_stays_and_shows_as_inactive()
    {
        await using var db = CreateDb();
        var (_, hr, finance) = await SeedAsync(db);
        var service = Service(db);
        var rule = await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("Rule", [], [], [hr.Id], null), default);
        hr.Status = UserAccountStatus.Deactivated;
        finance.Status = UserAccountStatus.Deactivated;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ReportDistributionValidationException>(() => service.UpdateRuleAsync(rule.Id, new SaveReportDistributionRuleRequest("Rule", [], [], [hr.Id, finance.Id], null), default));
        var kept = await service.UpdateRuleAsync(rule.Id, new SaveReportDistributionRuleRequest("Rule", ["FB"], [], [hr.Id], ["new@concord.lk"], IsActive: false), default);

        Assert.Equal([(hr.Id, true), (null, false)], kept.Recipients.Select(x => (x.UserId, x.Inactive)));
        Assert.Equal(("Factory B", false), (kept.FactoryLabel, kept.IsActive));
        Assert.Equal(2, await db.ReportDistributionRecipients.CountAsync());
    }

    [Fact]
    public async Task Preview_counts_each_active_rules_numbers_and_says_why_a_rule_cannot_be_sent()
    {
        await using var db = CreateDb();
        var (batchId, hr, _) = await SeedAsync(db);
        var service = Service(db);
        await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("A Factory A staff", ["FA"], ["ST"], [hr.Id], ["fa@concord.lk"]), default);
        await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("B All staff", [], ["ST"], null, ["staff@concord.lk"]), default);
        await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("C Factory B executives", ["FB"], ["EX"], null, ["fb@concord.lk"]), default);
        await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("D Everything", [], [], [hr.Id], null), default);
        await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("E Switched off", [], [], null, ["off@concord.lk"], IsActive: false), default);
        hr.Status = UserAccountStatus.Deactivated;
        await db.SaveChangesAsync();

        var preview = await service.PreviewAsync(batchId, default);

        Assert.Equal(["A Factory A staff", "B All staff", "C Factory B executives", "D Everything"], preview.Select(x => x.RuleName));
        Assert.Equal([1, 2, 0, 3], preview.Select(x => x.NumberCount));
        Assert.Equal([true, true, false, false], preview.Select(x => x.CanSend));
        Assert.Equal(["fa@concord.lk"], preview[0].Recipients);
        Assert.Equal("No numbers match this rule in the batch.", preview[2].Reason);
        Assert.Equal("No active recipients.", preview[3].Reason);
    }

    [Fact]
    public async Task Preview_is_refused_for_a_batch_that_is_not_completed_or_locked()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db, BillBatchStatus.FinanceApproval);

        await Assert.ThrowsAsync<BillingReportConflictException>(() => Service(db).PreviewAsync(batchId, default));
    }

    [Fact]
    public async Task Sending_queues_one_email_per_rule_with_its_filters_and_recipients_as_they_are_now()
    {
        await using var db = CreateDb();
        var (batchId, hr, _) = await SeedAsync(db);
        var service = Service(db);
        var rule = await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("Factory A", ["FA"], ["EX", "ST"], [hr.Id], ["fa@concord.lk"]), default);

        var queued = Assert.Single(await service.SendAsync(batchId, new SendReportEmailsRequest([rule.Id, rule.Id]), default));

        Assert.Equal((ReportEmailStatus.Pending, "Factory A", "hr@concord.lk; fa@concord.lk", "Pradeep Dissanayaka"), (queued.Status, queued.RuleName, queued.Recipients, queued.RequestedBy));
        var stored = await db.ReportEmailDeliveries.SingleAsync();
        Assert.Equal(("FA", "EX,ST", batchId), (stored.FactoryCodes, stored.CategoryCodes, stored.BillBatchId));
        Assert.Single(await service.ListDeliveriesAsync(batchId, default));
    }

    [Fact]
    public async Task Sending_is_refused_without_email_set_up_without_rules_or_for_a_rule_that_cannot_be_sent()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        var empty = await Service(db).CreateRuleAsync(new SaveReportDistributionRuleRequest("Factory B executives", ["FB"], ["EX"], null, ["fb@concord.lk"]), default);

        await Assert.ThrowsAsync<ReportDistributionConflictException>(() => Service(db, configured: false).SendAsync(batchId, new SendReportEmailsRequest([empty.Id]), default));
        await Assert.ThrowsAsync<ReportDistributionValidationException>(() => Service(db).SendAsync(batchId, new SendReportEmailsRequest([]), default));
        var refusal = await Assert.ThrowsAsync<ReportDistributionValidationException>(() => Service(db).SendAsync(batchId, new SendReportEmailsRequest([empty.Id]), default));
        Assert.Contains("No numbers match", refusal.Message);
        Assert.Empty(db.ReportEmailDeliveries);
    }

    [Fact]
    public async Task A_failed_email_can_be_sent_again_but_not_one_still_waiting()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        var delivery = new ReportEmailDelivery { BillBatchId = batchId, RuleName = "Rule", Recipients = "a@concord.lk", RequestedByName = "IT", Status = ReportEmailStatus.Failed, Attempts = 5, LastError = "Refused", NextAttemptAt = DateTimeOffset.UnixEpoch };
        db.ReportEmailDeliveries.Add(delivery);
        await db.SaveChangesAsync();

        var resent = await Service(db).ResendAsync(delivery.Id, default);

        Assert.Equal((ReportEmailStatus.Pending, 0, (string?)null), (resent.Status, resent.Attempts, resent.LastError));
        Assert.Null((await db.ReportEmailDeliveries.SingleAsync()).NextAttemptAt);
        await Assert.ThrowsAsync<ReportDistributionConflictException>(() => Service(db).ResendAsync(delivery.Id, default));
    }

    [Fact]
    public async Task Deleting_a_rule_keeps_its_sent_emails_in_the_log()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        var service = Service(db);
        var rule = await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("Factory A", ["FA"], [], null, ["fa@concord.lk"]), default);
        await service.SendAsync(batchId, new SendReportEmailsRequest([rule.Id]), default);

        await service.DeleteRuleAsync(rule.Id, default);

        Assert.Empty(db.ReportDistributionRules);
        Assert.Empty(db.ReportDistributionRecipients);
        var log = await db.ReportEmailDeliveries.SingleAsync();
        Assert.Equal((null, "Factory A"), (log.RuleId, log.RuleName));
    }

    [Fact]
    public async Task Recipient_options_are_the_active_users()
    {
        await using var db = CreateDb();
        var (_, hr, finance) = await SeedAsync(db);
        finance.Status = UserAccountStatus.Deactivated;
        await db.SaveChangesAsync();

        var options = await Service(db).GetRecipientOptionsAsync(default);

        Assert.Equal([(hr.Id, "HR Manager", "hr@concord.lk", UserRole.GroupHrManager)], options.Select(x => (x.UserId, x.DisplayName, x.Email, x.Role)));
    }

    [Fact]
    public async Task Locking_queues_every_active_rule_not_yet_emailed_for_the_batch_and_sends_failed_ones_again()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db, BillBatchStatus.Locked);
        var service = Service(db);
        var sentBefore = await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("A Factory A", ["FA"], [], null, ["fa@concord.lk"]), default);
        var failedBefore = await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("B Factory B", ["FB"], [], null, ["fb@concord.lk"]), default);
        await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("C Everything", [], [], null, ["cfo@concord.lk"]), default);
        await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("D Factory B executives", ["FB"], ["EX"], null, ["none@concord.lk"]), default);
        await service.CreateRuleAsync(new SaveReportDistributionRuleRequest("E Switched off", [], [], null, ["off@concord.lk"], IsActive: false), default);
        await service.SendAsync(batchId, new SendReportEmailsRequest([sentBefore.Id, failedBefore.Id]), default);
        (await db.ReportEmailDeliveries.SingleAsync(x => x.RuleId == failedBefore.Id)).Status = ReportEmailStatus.Failed;
        await db.SaveChangesAsync();

        await service.QueueOnLockAsync(batchId, default);

        var automatic = await db.ReportEmailDeliveries.Where(x => x.RequestedByName.StartsWith("Automatic")).OrderBy(x => x.RuleName).ToListAsync();
        Assert.Equal(["B Factory B", "C Everything"], automatic.Select(x => x.RuleName));
        Assert.All(automatic, x => Assert.Equal(("Automatic on lock (Pradeep Dissanayaka)", ReportEmailStatus.Pending), (x.RequestedByName, x.Status)));

        // Locking again (for example after an unlock) does not repeat them.
        await service.QueueOnLockAsync(batchId, default);
        Assert.Equal(4, await db.ReportEmailDeliveries.CountAsync());
    }

    [Fact]
    public async Task Locking_sends_nothing_while_email_or_the_option_is_off_and_never_throws()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db, BillBatchStatus.Locked);
        await Service(db).CreateRuleAsync(new SaveReportDistributionRuleRequest("Everything", [], [], null, ["cfo@concord.lk"]), default);

        await Service(db, configured: false).QueueOnLockAsync(batchId, default);
        await Service(db, sendOnLock: false).QueueOnLockAsync(batchId, default);
        // A batch the report cannot be built for (here an unknown one) is logged, not thrown.
        await Service(db).QueueOnLockAsync(Guid.NewGuid(), default);

        Assert.Empty(db.ReportEmailDeliveries);
    }

    [Fact]
    public async Task History_lists_every_batchs_emails_newest_first_with_the_batch_period_and_filters()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        var other = new BillBatch { ProviderId = Guid.NewGuid(), CorporateCode = "C", BillingYear = 2026, BillingMonth = 9, Status = BillBatchStatus.Locked };
        db.BillBatches.Add(other);
        db.ReportEmailDeliveries.AddRange(
            Delivery(batchId, "CAPL - Staff", "hr@concord.lk", ReportEmailStatus.Sent, 3),
            Delivery(batchId, "CKL - Workers", "ckl@concord.lk", ReportEmailStatus.Failed, 2),
            Delivery(other.Id, "CAPL - Staff", "hr@concord.lk; gm@concord.lk", ReportEmailStatus.Sent, 1));
        await db.SaveChangesAsync();
        var service = Service(db);

        var all = await service.SearchDeliveriesAsync(new ReportEmailHistoryRequest(), default);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal([(10, "CAPL - Staff"), (10, "CKL - Workers"), (9, "CAPL - Staff")], all.Items.Select(x => (x.BillingMonth, x.RuleName)));
        Assert.Equal(2026, all.Items[0].BillingYear);

        Assert.Equal(["CKL - Workers"], (await service.SearchDeliveriesAsync(new ReportEmailHistoryRequest(Status: ReportEmailStatus.Failed), default)).Items.Select(x => x.RuleName));
        Assert.Equal(2, (await service.SearchDeliveriesAsync(new ReportEmailHistoryRequest(Search: "hr@concord"), default)).TotalCount);
        Assert.Equal(2, (await service.SearchDeliveriesAsync(new ReportEmailHistoryRequest(BatchId: batchId), default)).TotalCount);
        var page = await service.SearchDeliveriesAsync(new ReportEmailHistoryRequest(PageNumber: 2, PageSize: 2), default);
        Assert.Equal((3, 2, 2, 1), (page.TotalCount, page.PageNumber, page.PageSize, page.Items.Count));
    }

    private static ReportEmailDelivery Delivery(Guid batchId, string rule, string recipients, ReportEmailStatus status, int hour) =>
        new() { BillBatchId = batchId, RuleName = rule, Recipients = recipients, RequestedByName = "IT", Status = status, CreatedAtUtc = new DateTimeOffset(2026, 11, 9, hour, 0, 0, TimeSpan.Zero) };

    private static EfReportDistributionService Service(MobileBillDbContext db, bool configured = true, bool sendOnLock = true) =>
        new(db, new TestUser(), new TestClock(), new RecordingSender(configured), new ReportEmailWakeUp(),
            Microsoft.Extensions.Options.Options.Create(new MobileBill.Infrastructure.Notifications.EmailOptions { SendReportsOnLock = sendOnLock }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EfReportDistributionService>.Instance);
}
