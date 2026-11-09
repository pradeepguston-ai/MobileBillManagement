using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MobileBill.Application.Notifications;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Notifications;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Notifications;

public sealed class BillApprovalEmailNotifierTests
{
    [Theory]
    [InlineData(BillBatchStatus.ITReview, "hod.it@concord.lk", "IT approval", "Head of IT")]
    [InlineData(BillBatchStatus.HRApproval, "hr.manager@concord.lk", "HR approval", "Group HR Manager")]
    [InlineData(BillBatchStatus.FinanceApproval, "cfo@concord.lk", "Finance approval", "CFO")]
    public async Task Emails_the_active_approvers_of_the_step_the_batch_is_waiting_for(BillBatchStatus status, string recipient, string step, string approver)
    {
        await using var db = CreateDb();
        var batch = await SeedAsync(db, status);
        var queue = new TestQueue();

        await Notifier(db, queue).NotifyAwaitingApprovalAsync(batch.Id, default);

        var message = Assert.Single(queue.Messages);
        Assert.Equal([recipient], message.To);
        Assert.Equal($"[Mobile Billing System] October 2026 mobile bill awaiting {step}", message.Subject);
        Assert.Contains($"Dear {approver},", message.HtmlBody);
        Assert.Contains("Dialog Axiata", message.HtmlBody);
        Assert.Contains("CORP-1", message.HtmlBody);
        Assert.Contains("<td style=\"border:1px solid #d0d7de\">2</td>", message.HtmlBody);
        Assert.Contains("https://mobilebill.example/billing/" + batch.Id + "/review", message.HtmlBody);
        Assert.Contains(BillApprovalEmailNotifier.Footer, message.HtmlBody);
    }

    [Fact]
    public async Task Names_the_last_step_with_who_did_it_when_in_Sri_Lanka_time_and_the_comment_encoded()
    {
        await using var db = CreateDb();
        var batch = await SeedAsync(db, BillBatchStatus.HRApproval);
        db.ApprovalHistories.Add(History(batch.Id, ApprovalStage.ITReview, ApprovalAction.Submit, "Pradeep", new DateTimeOffset(2026, 11, 2, 5, 0, 0, TimeSpan.Zero), "Old"));
        db.ApprovalHistories.Add(History(batch.Id, ApprovalStage.ITReview, ApprovalAction.Approve, "Pubudu", new DateTimeOffset(2026, 11, 3, 6, 25, 0, TimeSpan.Zero), "Checked <all> numbers"));
        await db.SaveChangesAsync();
        var queue = new TestQueue();

        await Notifier(db, queue).NotifyAwaitingApprovalAsync(batch.Id, default);

        var body = Assert.Single(queue.Messages).HtmlBody;
        Assert.Contains("IT approved by Pubudu on 03 Nov 2026, 11:55 AM", body);
        Assert.Contains("Checked &lt;all&gt; numbers", body);
        Assert.DoesNotContain("Pradeep", body);
    }

    [Fact]
    public async Task Test_recipient_receives_every_email_and_sees_who_it_would_have_gone_to()
    {
        await using var db = CreateDb();
        var batch = await SeedAsync(db, BillBatchStatus.FinanceApproval);
        var queue = new TestQueue();

        await Notifier(db, queue, new EmailOptions { Enabled = true, TestRecipient = "it.tester@concord.lk" }).NotifyAwaitingApprovalAsync(batch.Id, default);

        var message = Assert.Single(queue.Messages);
        Assert.Equal(["it.tester@concord.lk"], message.To);
        Assert.Contains("Test mode.", message.HtmlBody);
        Assert.Contains("cfo@concord.lk", message.HtmlBody);
    }

    [Theory]
    [InlineData(BillBatchStatus.Validated)]
    [InlineData(BillBatchStatus.Completed)]
    [InlineData(BillBatchStatus.Locked)]
    public async Task Sends_nothing_when_the_batch_is_not_waiting_for_an_approver(BillBatchStatus status)
    {
        await using var db = CreateDb();
        var batch = await SeedAsync(db, status);
        var queue = new TestQueue();

        await Notifier(db, queue).NotifyAwaitingApprovalAsync(batch.Id, default);

        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task Sends_nothing_until_email_is_enabled_with_its_credentials()
    {
        await using var db = CreateDb();
        var batch = await SeedAsync(db, BillBatchStatus.ITReview);
        var queue = new TestQueue();

        await Notifier(db, queue, new EmailOptions { Enabled = true, TenantId = "t", ClientId = "c", ClientSecret = "" }).NotifyAwaitingApprovalAsync(batch.Id, default);
        await Notifier(db, queue, new EmailOptions { Enabled = false, TenantId = "t", ClientId = "c", ClientSecret = "s" }).NotifyAwaitingApprovalAsync(batch.Id, default);

        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task Sends_nothing_when_no_active_approver_has_an_email_address()
    {
        await using var db = CreateDb();
        var batch = await SeedAsync(db, BillBatchStatus.ITReview);
        foreach (var user in db.Users.Where(x => x.Role == UserRole.HeadOfIt)) user.Status = UserAccountStatus.Deactivated;
        await db.SaveChangesAsync();
        var queue = new TestQueue();

        await Notifier(db, queue).NotifyAwaitingApprovalAsync(batch.Id, default);

        Assert.Empty(queue.Messages);
    }

    private static BillApprovalEmailNotifier Notifier(MobileBillDbContext db, IEmailQueue queue, EmailOptions? overrides = null)
    {
        var options = overrides ?? new EmailOptions();
        options.Enabled = overrides?.Enabled ?? true;
        options.TenantId ??= "tenant"; options.ClientId ??= "client"; options.ClientSecret ??= "secret";
        options.AppBaseUrl ??= "https://mobilebill.example/";
        return new BillApprovalEmailNotifier(db, queue, Options.Create(options), NullLogger<BillApprovalEmailNotifier>.Instance);
    }

    private static MobileBillDbContext CreateDb() => new(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<BillBatch> SeedAsync(MobileBillDbContext db, BillBatchStatus status)
    {
        var provider = new TelecomProvider { Code = "DIALOG", Name = "Dialog Axiata" };
        var batch = new BillBatch { ProviderId = provider.Id, Provider = provider, CorporateCode = "CORP-1", BillingYear = 2026, BillingMonth = 10, Status = status };
        db.BillBatches.Add(batch);
        db.BillLines.AddRange(new BillLine { BillBatchId = batch.Id, MobileNumber = "1", RawText = "1" }, new BillLine { BillBatchId = batch.Id, MobileNumber = "2", RawText = "2" });
        db.Users.AddRange(
            User("hod.it@concord.lk", UserRole.HeadOfIt, UserAccountStatus.Active),
            User("old.hod@concord.lk", UserRole.HeadOfIt, UserAccountStatus.Deactivated),
            User("hr.manager@concord.lk", UserRole.GroupHrManager, UserAccountStatus.Active),
            User("cfo@concord.lk", UserRole.Cfo, UserAccountStatus.Active),
            User("admin@concord.lk", UserRole.Administrator, UserAccountStatus.Active),
            User("engineer@concord.lk", UserRole.ITEngineer, UserAccountStatus.Active));
        await db.SaveChangesAsync();
        return batch;
    }

    private static User User(string email, UserRole role, UserAccountStatus status) => new() { Email = email, DisplayName = email, PasswordHash = "x", Role = role, Status = status };
    private static ApprovalHistory History(Guid batchId, ApprovalStage stage, ApprovalAction action, string name, DateTimeOffset at, string comment) =>
        new() { BillBatchId = batchId, Stage = stage, Action = action, UserId = name, DisplayName = name, ApprovedBy = name, ApprovedAt = at, Comment = comment };

    private sealed class TestQueue : IEmailQueue
    {
        public List<EmailMessage> Messages { get; } = [];
        public void Enqueue(EmailMessage message) => Messages.Add(message);
        public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
