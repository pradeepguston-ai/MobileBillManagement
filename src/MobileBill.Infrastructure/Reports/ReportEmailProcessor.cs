using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobileBill.Application.Common;
using MobileBill.Application.Notifications;
using MobileBill.Application.Reports;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Notifications;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Reports;

// Sends the oldest due report email: builds the rule's Excel and PDF reports (the same files the download gives), attaches
// them and sends. A busy or unreachable mail service is tried again later; anything else marks the email Failed with the reason.
public sealed class ReportEmailProcessor(MobileBillDbContext db, IBillingExcelReportService excelReports, IBillingPdfReportService pdfReports,
    IEmailSender sender, IOptions<EmailOptions> options, IClock clock, ILogger<ReportEmailProcessor> logger) : IReportEmailProcessor
{
    private readonly EmailOptions settings = options.Value;

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var delivery = (await db.ReportEmailDeliveries
                .Where(item => item.Status == ReportEmailStatus.Pending)
                .ToListAsync(cancellationToken))
            .Where(item => item.NextAttemptAt is null || item.NextAttemptAt <= now)
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.RuleName)
            .FirstOrDefault();
        if (delivery is null) return false;

        delivery.Attempts++;
        delivery.UpdatedAtUtc = now;
        try
        {
            var message = await BuildAsync(delivery, cancellationToken);
            await sender.SendAsync(message, cancellationToken);
            delivery.Status = ReportEmailStatus.Sent;
            delivery.SentAt = clock.UtcNow;
            delivery.NextAttemptAt = null;
            delivery.LastError = null;
            logger.LogInformation("Sent the report email \"{Rule}\" for batch {BatchId}.", delivery.RuleName, delivery.BillBatchId);
        }
        catch (EmailDeliveryException exception) when (exception.IsTransient && delivery.Attempts < Math.Max(1, settings.MaxAttempts))
        {
            var delay = exception.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(Math.Max(0, settings.RetryDelaySeconds) * delivery.Attempts);
            delivery.NextAttemptAt = clock.UtcNow + delay;
            delivery.LastError = Truncate($"Attempt {delivery.Attempts} failed: {exception.Message} Trying again.");
            logger.LogWarning("Report email \"{Rule}\" failed (attempt {Attempt}): {Reason}", delivery.RuleName, delivery.Attempts, exception.Message);
        }
        catch (Exception exception) when (exception is EmailDeliveryException or ReportEmailException or BillingReportConflictException or BillingReportNotFoundException or BillingReportFilterNotFoundException)
        {
            Fail(delivery, exception.Message);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Report email \"{Rule}\" for batch {BatchId} could not be sent.", delivery.RuleName, delivery.BillBatchId);
            Fail(delivery, "The report could not be built or sent. See the API log for details.");
        }
        await db.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    private void Fail(ReportEmailDelivery delivery, string reason)
    {
        delivery.Status = ReportEmailStatus.Failed;
        delivery.NextAttemptAt = null;
        delivery.LastError = Truncate(reason);
        logger.LogWarning("Report email \"{Rule}\" for batch {BatchId} failed: {Reason}", delivery.RuleName, delivery.BillBatchId, reason);
    }

    public async Task<EmailMessage> BuildAsync(ReportEmailDelivery delivery, CancellationToken cancellationToken)
    {
        var factories = EfReportDistributionService.Codes(delivery.FactoryCodes);
        var categories = EfReportDistributionService.Codes(delivery.CategoryCodes);
        var report = await BillingReportData.LoadAsync(db, delivery.BillBatchId, cancellationToken, factories, categories);
        if (report.Entries.Count == 0) throw new ReportEmailException("No numbers match this rule in the batch.");
        var request = new BillingExcelReportRequest(delivery.BillBatchId, factories, categories);
        var excel = await excelReports.ExportAsync(request, cancellationToken);
        var pdf = await pdfReports.ExportAsync(request, cancellationToken);

        var recipients = delivery.Recipients.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (recipients.Count == 0) throw new ReportEmailException("The email has no recipients.");
        var period = $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(report.BillingMonth)} {report.BillingYear}";
        var subject = $"[{BillApprovalEmailNotifier.SystemName}] {period} Mobile Bill Report - {delivery.RuleName}";
        var rows = new (string Label, string Value)[]
        {
            ("Billing period", period),
            ("Provider", report.ProviderName),
            ("Factories", report.FactoryLabel),
            ("Categories", report.CategoryLabel),
            ("Numbers", report.Entries.Count.ToString("N0", CultureInfo.InvariantCulture)),
            ("Total bill", Money(report.ExportedActualBillTotal)),
            ("Deducted from employees (By User)", Money(report.DeductedFromEmployees)),
            ("Borne by company (By Company)", Money(report.BorneByCompany)),
        };

        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#1f2933\">");
        if (!string.IsNullOrWhiteSpace(settings.TestRecipient))
            html.Append($"<p style=\"background:#fff4ce;padding:8px;border:1px solid #e0c060\"><b>Test mode.</b> In normal use this email goes to: {Encode(string.Join(", ", recipients))}</p>");
        html.Append("<p>Dear colleague,</p>");
        html.Append($"<p>Please find attached the <b>{Encode(period)}</b> mobile bill report for <b>{Encode(delivery.RuleName)}</b>, as Excel and PDF.</p>");
        html.Append("<table cellpadding=\"6\" style=\"border-collapse:collapse;font-size:14px\">");
        foreach (var (label, value) in rows)
            html.Append($"<tr><td style=\"border:1px solid #d0d7de;background:#f3f6f9;font-weight:bold\">{Encode(label)}</td><td style=\"border:1px solid #d0d7de\">{Encode(value)}</td></tr>");
        html.Append("</table>");
        html.Append($"<hr style=\"border:none;border-top:1px solid #d0d7de\"><p style=\"font-size:12px;color:#6b7785\">{BillApprovalEmailNotifier.Footer}</p></div>");

        IReadOnlyList<string> to = string.IsNullOrWhiteSpace(settings.TestRecipient) ? recipients : [settings.TestRecipient.Trim()];
        return new EmailMessage(to, subject, html.ToString(), [
            new EmailAttachment(excel.FileName, excel.ContentType, excel.Content),
            new EmailAttachment(pdf.FileName, pdf.ContentType, pdf.Content),
        ]);
    }

    private static string Money(decimal value) => $"LKR {value.ToString("N2", CultureInfo.InvariantCulture)}";
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}

// The report email cannot be sent as it stands (for example no numbers match); sending again will not help until something changes.
public sealed class ReportEmailException(string message) : Exception(message);
