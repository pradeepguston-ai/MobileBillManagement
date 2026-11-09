using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobileBill.Application.Notifications;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Notifications;

// When a batch reaches an approval step, emails the active users who approve it: IT review → Head of IT,
// HR approval → Group HR Manager, Finance approval → CFO. The email names the batch, what was done before it and links to it;
// it carries no amounts or employee details.
public sealed class BillApprovalEmailNotifier(MobileBillDbContext db, IEmailQueue queue, IOptions<EmailOptions> options, ILogger<BillApprovalEmailNotifier> logger) : IBillApprovalNotifier
{
    public const string SystemName = "Mobile Billing System";
    public const string Footer = "This is an automated message from the Mobile Billing System. Please do not reply.";
    // Times are shown in Sri Lanka time, which has no daylight saving.
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromHours(5.5);
    private readonly EmailOptions settings = options.Value;

    public static (UserRole Role, string Approver, string Step)? ApproverFor(BillBatchStatus status) => status switch
    {
        BillBatchStatus.ITReview => (UserRole.HeadOfIt, "Head of IT", "IT approval"),
        BillBatchStatus.HRApproval => (UserRole.GroupHrManager, "Group HR Manager", "HR approval"),
        BillBatchStatus.FinanceApproval => (UserRole.Cfo, "CFO", "Finance approval"),
        _ => null,
    };

    public async Task NotifyAwaitingApprovalAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured) return;
        try
        {
            var message = await BuildAsync(batchId, cancellationToken);
            if (message is not null) queue.Enqueue(message);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "The approval email for batch {BatchId} could not be prepared.", batchId);
        }
    }

    public async Task<EmailMessage?> BuildAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await db.BillBatches.AsNoTracking()
            .Where(x => x.Id == batchId)
            .Select(x => new { x.Id, x.Status, x.BillingYear, x.BillingMonth, x.CorporateCode, Provider = x.Provider.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (batch is null || ApproverFor(batch.Status) is not { } approver) return null;

        var recipients = await db.Users.AsNoTracking()
            .Where(user => user.Role == approver.Role && user.Status == UserAccountStatus.Active)
            .Select(user => user.Email).ToListAsync(cancellationToken);
        recipients = recipients.Where(email => email.Contains('@')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (recipients.Count == 0)
        {
            logger.LogWarning("Batch {BatchId} is waiting for {Approver} but no active {Approver} user has an email address.", batchId, approver.Approver, approver.Approver);
            return null;
        }

        var numbers = await db.BillLines.CountAsync(x => x.BillBatchId == batchId, cancellationToken);
        var latest = (await db.ApprovalHistories.AsNoTracking().Where(x => x.BillBatchId == batchId).ToListAsync(cancellationToken))
            .OrderByDescending(x => x.ApprovedAt).FirstOrDefault();
        var period = $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(batch.BillingMonth)} {batch.BillingYear}";
        var subject = $"[{SystemName}] {period} mobile bill awaiting {approver.Step}";

        var rows = new List<(string Label, string Value)>
        {
            ("Billing period", period),
            ("Provider", batch.Provider),
            ("Corporate account", batch.CorporateCode),
            ("Numbers on the bill", numbers.ToString("N0", CultureInfo.InvariantCulture)),
            ("Waiting for", $"{approver.Step} ({approver.Approver})"),
        };
        if (latest is not null)
        {
            rows.Add(("Last step", $"{PreviousStep(latest)} by {latest.DisplayName} on {latest.ApprovedAt.ToOffset(SriLankaOffset).ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture)}"));
            if (!string.IsNullOrWhiteSpace(latest.Comment)) rows.Add(("Comment", latest.Comment.Trim()));
        }

        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#1f2933\">");
        if (!string.IsNullOrWhiteSpace(settings.TestRecipient))
            html.Append($"<p style=\"background:#fff4ce;padding:8px;border:1px solid #e0c060\"><b>Test mode.</b> In normal use this email goes to: {Encode(string.Join(", ", recipients))}</p>");
        html.Append($"<p>Dear {Encode(approver.Approver)},</p>");
        html.Append($"<p>The <b>{Encode(period)}</b> mobile bill batch is waiting for your {Encode(approver.Step)} in the {SystemName}.</p>");
        html.Append("<table cellpadding=\"6\" style=\"border-collapse:collapse;font-size:14px\">");
        foreach (var (label, value) in rows)
            html.Append($"<tr><td style=\"border:1px solid #d0d7de;background:#f3f6f9;font-weight:bold\">{Encode(label)}</td><td style=\"border:1px solid #d0d7de\">{Encode(value)}</td></tr>");
        html.Append("</table>");
        if (ReviewLink(batch.Id) is { } link)
            html.Append($"<p><a href=\"{Encode(link)}\" style=\"display:inline-block;background:#1f4e78;color:#ffffff;padding:10px 16px;text-decoration:none;border-radius:4px\">Open the bill review</a></p>");
        else
            html.Append($"<p>Sign in to the {SystemName} and open Monthly Bill Review to review and approve it.</p>");
        html.Append($"<hr style=\"border:none;border-top:1px solid #d0d7de\"><p style=\"font-size:12px;color:#6b7785\">{Footer}</p></div>");

        IReadOnlyList<string> to = string.IsNullOrWhiteSpace(settings.TestRecipient) ? recipients : [settings.TestRecipient.Trim()];
        return new EmailMessage(to, subject, html.ToString());
    }

    private string? ReviewLink(Guid batchId) =>
        string.IsNullOrWhiteSpace(settings.AppBaseUrl) ? null : $"{settings.AppBaseUrl.TrimEnd('/')}/billing/{batchId}/review";

    private static string PreviousStep(ApprovalHistory history) => (history.Stage, history.Action) switch
    {
        (ApprovalStage.ITReview, ApprovalAction.Submit) => "Submitted for IT review",
        (ApprovalStage.ITReview, ApprovalAction.Approve) => "IT approved",
        (ApprovalStage.HRApproval, ApprovalAction.Approve) => "HR approved",
        (ApprovalStage.FinanceApproval, ApprovalAction.Approve) => "Finance approved",
        _ => history.Action.ToString(),
    };

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
