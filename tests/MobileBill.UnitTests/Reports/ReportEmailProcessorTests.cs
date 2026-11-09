using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MobileBill.Application.Notifications;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Notifications;
using MobileBill.Infrastructure.Persistence;
using MobileBill.Infrastructure.Reports;
using static MobileBill.UnitTests.Reports.ReportDistributionTestData;

namespace MobileBill.UnitTests.Reports;

public sealed class ReportEmailProcessorTests
{
    [Fact]
    public async Task Sends_the_rules_excel_and_pdf_reports_to_its_recipients_and_marks_the_email_sent()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        var delivery = await QueueAsync(db, batchId, "FA", "EX,ST");
        var sender = new RecordingSender();
        var clock = new TestClock();

        Assert.True(await Processor(db, sender, clock).ProcessNextAsync(default));

        var message = Assert.Single(sender.Sent);
        Assert.Equal(["manager.fa@concord.lk", "hr@concord.lk"], message.To);
        Assert.Equal("[Mobile Billing System] October 2026 Mobile Bill Report - Factory A", message.Subject);
        Assert.Equal(["Mobile_Bill_Report_2026_10_FA_Executive-Staff.xlsx", "Mobile_Bill_Report_2026_10_FA_Executive-Staff.pdf"], message.Attachments!.Select(x => x.FileName));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(message.Attachments![1].Content, 0, 4));
        using (var workbook = new XLWorkbook(new MemoryStream(message.Attachments![0].Content)))
            Assert.Equal(["0771000001", "0771000002"], workbook.Worksheet("Monthly Bill Report").Table("MonthlyBillReportTable").DataRange!.Rows().Select(row => row.Cell(2).GetString()));
        foreach (var text in new[] { "Factory A (FA)", "Executive, Staff", "<td style=\"border:1px solid #d0d7de\">2</td>", "LKR 150.00", "LKR 20.00", BillApprovalEmailNotifier.Footer })
            Assert.Contains(text, message.HtmlBody);
        Assert.DoesNotContain("Test mode", message.HtmlBody);

        var stored = await db.ReportEmailDeliveries.SingleAsync();
        Assert.Equal((ReportEmailStatus.Sent, 1, clock.UtcNow, (string?)null), (stored.Status, stored.Attempts, stored.SentAt, stored.LastError));
        Assert.False(await Processor(db, sender, clock).ProcessNextAsync(default));
        _ = delivery;
    }

    [Fact]
    public async Task In_test_mode_the_report_goes_to_the_test_recipient_and_names_the_real_recipients()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        await QueueAsync(db, batchId, "", "");
        var sender = new RecordingSender();

        await Processor(db, sender, new TestClock(), new EmailOptions { TestRecipient = "it.tester@concord.lk" }).ProcessNextAsync(default);

        var message = Assert.Single(sender.Sent);
        Assert.Equal(["it.tester@concord.lk"], message.To);
        Assert.Contains("Test mode.", message.HtmlBody);
        Assert.Contains("manager.fa@concord.lk, hr@concord.lk", message.HtmlBody);
    }

    [Fact]
    public async Task A_busy_mail_service_is_tried_again_later_and_gives_up_after_the_last_attempt()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        await QueueAsync(db, batchId, "FB", "");
        var busy = new EmailDeliveryException("Busy", isTransient: true);
        var sender = new RecordingSender(true, busy, busy);
        var clock = new TestClock();
        var options = new EmailOptions { MaxAttempts = 2, RetryDelaySeconds = 60 };

        await Processor(db, sender, clock, options).ProcessNextAsync(default);
        var waiting = await db.ReportEmailDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal((ReportEmailStatus.Pending, 1, clock.UtcNow.AddSeconds(60)), (waiting.Status, waiting.Attempts, waiting.NextAttemptAt));
        Assert.Contains("Attempt 1 failed: Busy", waiting.LastError);

        // Not due yet.
        Assert.False(await Processor(db, sender, clock, options).ProcessNextAsync(default));
        clock.UtcNow = clock.UtcNow.AddSeconds(60);
        Assert.True(await Processor(db, sender, clock, options).ProcessNextAsync(default));

        var failed = await db.ReportEmailDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal((ReportEmailStatus.Failed, 2, "Busy"), (failed.Status, failed.Attempts, failed.LastError));
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task A_refusal_that_will_not_change_fails_at_once_with_the_reason()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        await QueueAsync(db, batchId, "", "");
        var sender = new RecordingSender(true, new EmailDeliveryException("The attachments are 3.1 MB; an email can carry at most 2.8 MB.", isTransient: false));

        await Processor(db, sender, new TestClock()).ProcessNextAsync(default);

        var failed = await db.ReportEmailDeliveries.SingleAsync();
        Assert.Equal((ReportEmailStatus.Failed, 1), (failed.Status, failed.Attempts));
        Assert.StartsWith("The attachments are 3.1 MB", failed.LastError);
    }

    [Fact]
    public async Task A_report_that_can_no_longer_be_built_fails_with_the_reason()
    {
        await using var db = CreateDb();
        var (batchId, _, _) = await SeedAsync(db);
        await QueueAsync(db, batchId, "FB", "EX");
        await QueueAsync(db, batchId, "GONE", "");

        var processor = Processor(db, new RecordingSender(), new TestClock());
        await processor.ProcessNextAsync(default);
        await processor.ProcessNextAsync(default);

        var errors = await db.ReportEmailDeliveries.OrderBy(x => x.FactoryCodes).Select(x => new { x.Status, x.LastError }).ToListAsync();
        Assert.All(errors, x => Assert.Equal(ReportEmailStatus.Failed, x.Status));
        Assert.Equal("No numbers match this rule in the batch.", errors[0].LastError);
        Assert.Contains("GONE", errors[1].LastError);
    }

    private static async Task<ReportEmailDelivery> QueueAsync(MobileBillDbContext db, Guid batchId, string factories, string categories)
    {
        var delivery = new ReportEmailDelivery
        {
            BillBatchId = batchId, RuleName = "Factory A", FactoryCodes = factories, CategoryCodes = categories,
            Recipients = "manager.fa@concord.lk; hr@concord.lk", RequestedByName = "IT", CreatedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(db.ReportEmailDeliveries.Count()),
        };
        db.ReportEmailDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        return delivery;
    }

    private static ReportEmailProcessor Processor(MobileBillDbContext db, IEmailSender sender, TestClock clock, EmailOptions? options = null) =>
        new(db, new ClosedXmlBillingExcelReportService(db, clock, null!), new PdfBillingReportService(db, clock), sender,
            Options.Create(options ?? new EmailOptions()), clock, NullLogger<ReportEmailProcessor>.Instance);
}
