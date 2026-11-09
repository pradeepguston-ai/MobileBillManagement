using System.Net.Mail;
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

// The report distribution rules, and sending a batch's report to each rule's recipients. Sending only queues the emails
// (ReportEmailDeliveries); ReportEmailProcessor builds the Excel and PDF reports and sends them in the background.
public sealed class EfReportDistributionService(MobileBillDbContext db, ICurrentUserService currentUser, IClock clock, IEmailSender emailSender, ReportEmailWakeUp wakeUp,
    IOptions<EmailOptions> emailOptions, ILogger<EfReportDistributionService> logger) : IReportDistributionService, IReportLockDistribution
{
    private const int MaxNameLength = 200;
    private const int MaxEmailLength = 256;

    public async Task<IReadOnlyList<ReportDistributionRuleDto>> ListRulesAsync(CancellationToken cancellationToken)
    {
        var rules = await RulesWithRecipients().OrderBy(rule => rule.Name).ToListAsync(cancellationToken);
        var names = await MasterNamesAsync(cancellationToken);
        return rules.Select(rule => Map(rule, names)).ToList();
    }

    public async Task<IReadOnlyList<ReportRecipientOptionDto>> GetRecipientOptionsAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(user => user.Status == UserAccountStatus.Active)
            .OrderBy(user => user.DisplayName)
            .Select(user => new ReportRecipientOptionDto(user.Id, user.DisplayName, user.Email, user.Role))
            .ToListAsync(cancellationToken);

    public async Task<ReportDistributionRuleDto> CreateRuleAsync(SaveReportDistributionRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = new ReportDistributionRule { Name = string.Empty, CreatedAtUtc = clock.UtcNow, CreatedBy = currentUser.UserId };
        await ApplyAsync(rule, request, cancellationToken);
        db.ReportDistributionRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return await GetRuleAsync(rule.Id, cancellationToken);
    }

    public async Task<ReportDistributionRuleDto> UpdateRuleAsync(Guid id, SaveReportDistributionRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await db.ReportDistributionRules.Include(item => item.Recipients).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ReportDistributionNotFoundException("The report distribution rule was not found.");
        await ApplyAsync(rule, request, cancellationToken);
        rule.UpdatedAtUtc = clock.UtcNow;
        rule.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetRuleAsync(rule.Id, cancellationToken);
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await db.ReportDistributionRules.Include(item => item.Recipients).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ReportDistributionNotFoundException("The report distribution rule was not found.");
        // Emails already sent for the rule keep its name in the log.
        foreach (var delivery in await db.ReportEmailDeliveries.Where(item => item.RuleId == id).ToListAsync(cancellationToken)) delivery.RuleId = null;
        db.ReportDistributionRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReportEmailPreviewDto>> PreviewAsync(Guid batchId, CancellationToken cancellationToken)
    {
        // The same report the download builds (it refuses batches that are not Completed or Locked, or not fully assessed).
        var report = await BillingReportData.LoadAsync(db, batchId, cancellationToken);
        var rules = await RulesWithRecipients().Where(rule => rule.IsActive).OrderBy(rule => rule.Name).ToListAsync(cancellationToken);
        var names = await MasterNamesAsync(cancellationToken);
        return rules.Select(rule =>
        {
            var factories = Codes(rule.FactoryCodes);
            var categories = Codes(rule.CategoryCodes);
            var count = factories.Count == 0 && categories.Count == 0
                ? report.Entries.Count
                : report.Entries.Count(entry => entry.Bill is { } bill && (factories.Count == 0 || factories.Contains(bill.FactoryCode)) && (categories.Count == 0 || categories.Contains(bill.CategoryCode)));
            var recipients = ActiveRecipients(rule);
            var reason = recipients.Count == 0 ? "No active recipients." : count == 0 ? "No numbers match this rule in the batch." : null;
            return new ReportEmailPreviewDto(rule.Id, rule.Name, Label(factories, names.Factories, "All factories"), Label(categories, names.Categories, "All categories"), recipients, count, reason is null, reason);
        }).ToList();
    }

    public async Task<IReadOnlyList<ReportEmailDeliveryDto>> SendAsync(Guid batchId, SendReportEmailsRequest request, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured) throw new ReportDistributionConflictException("Email is not set up. Ask an administrator to configure the email settings.");
        var ruleIds = (request.RuleIds ?? []).Distinct().ToList();
        if (ruleIds.Count == 0) throw new ReportDistributionValidationException("Choose at least one rule to send.");
        var preview = await PreviewAsync(batchId, cancellationToken);
        return await QueueAsync(batchId, ruleIds, preview, currentUser.DisplayName, cancellationToken);
    }

    public async Task QueueOnLockAsync(Guid batchId, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured || !emailOptions.Value.SendReportsOnLock) return;
        try
        {
            var preview = await PreviewAsync(batchId, cancellationToken);
            // Rules already emailed for this batch (sent, or still waiting) are not sent twice; one that failed is sent again.
            var alreadyEmailed = await db.ReportEmailDeliveries
                .Where(item => item.BillBatchId == batchId && item.RuleId != null && item.Status != ReportEmailStatus.Failed)
                .Select(item => item.RuleId!.Value).ToListAsync(cancellationToken);
            var ruleIds = preview.Where(item => item.CanSend && !alreadyEmailed.Contains(item.RuleId)).Select(item => item.RuleId).ToList();
            if (ruleIds.Count == 0) return;
            var queued = await QueueAsync(batchId, ruleIds, preview, $"Automatic on lock ({currentUser.DisplayName})", cancellationToken);
            logger.LogInformation("Queued {Count} report email(s) for batch {BatchId} on lock.", queued.Count, batchId);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "The report emails for locked batch {BatchId} could not be queued; send them from Monthly Bill Report.", batchId);
        }
    }

    public async Task<PagedResult<ReportEmailDeliveryDto>> SearchDeliveriesAsync(ReportEmailHistoryRequest request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var pageNumber = Math.Max(1, request.PageNumber);
        var query = db.ReportEmailDeliveries.AsNoTracking();
        if (request.BatchId is { } batchId) query = query.Where(item => item.BillBatchId == batchId);
        if (request.Status is { } status) query = query.Where(item => item.Status == status);
        if (request.Search?.Trim() is { Length: > 0 } search) query = query.Where(item => item.RuleName.Contains(search) || item.Recipients.Contains(search));
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.RuleName)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(item => new ReportEmailDeliveryDto(item.Id, item.RuleId, item.RuleName, item.Recipients, item.Status, item.Attempts, item.CreatedAtUtc, item.RequestedByName, item.SentAt, item.LastError,
                item.BillBatchId, item.BillBatch.BillingYear, item.BillBatch.BillingMonth))
            .ToListAsync(cancellationToken);
        return new PagedResult<ReportEmailDeliveryDto>(items, pageNumber, pageSize, total);
    }

    private async Task<IReadOnlyList<ReportEmailDeliveryDto>> QueueAsync(Guid batchId, IReadOnlyList<Guid> ruleIds, IReadOnlyList<ReportEmailPreviewDto> previewItems, string requestedBy, CancellationToken cancellationToken)
    {
        var preview = previewItems.ToDictionary(item => item.RuleId);
        var rules = await db.ReportDistributionRules.AsNoTracking().Where(rule => ruleIds.Contains(rule.Id)).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        var deliveries = new List<ReportEmailDelivery>();
        foreach (var id in ruleIds)
        {
            var rule = rules.SingleOrDefault(item => item.Id == id);
            if (rule is null || !preview.TryGetValue(id, out var item)) throw new ReportDistributionValidationException("A chosen rule was not found or is not active.");
            if (!item.CanSend) throw new ReportDistributionValidationException($"\"{rule.Name}\" cannot be sent: {item.Reason}");
            deliveries.Add(new ReportEmailDelivery
            {
                BillBatchId = batchId, RuleId = rule.Id, RuleName = rule.Name, FactoryCodes = rule.FactoryCodes, CategoryCodes = rule.CategoryCodes,
                Recipients = string.Join("; ", item.Recipients), RequestedByName = requestedBy, CreatedAtUtc = now, CreatedBy = currentUser.UserId,
            });
        }
        db.ReportEmailDeliveries.AddRange(deliveries);
        await db.SaveChangesAsync(cancellationToken);
        wakeUp.Signal();
        return deliveries.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ReportEmailDeliveryDto>> ListDeliveriesAsync(Guid batchId, CancellationToken cancellationToken) =>
        (await db.ReportEmailDeliveries.AsNoTracking().Where(item => item.BillBatchId == batchId).OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.RuleName).ToListAsync(cancellationToken))
            .Select(Map).ToList();

    public async Task<ReportEmailDeliveryDto> ResendAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured) throw new ReportDistributionConflictException("Email is not set up. Ask an administrator to configure the email settings.");
        var delivery = await db.ReportEmailDeliveries.SingleOrDefaultAsync(item => item.Id == deliveryId, cancellationToken)
            ?? throw new ReportDistributionNotFoundException("The report email was not found.");
        if (delivery.Status == ReportEmailStatus.Pending) throw new ReportDistributionConflictException("This report email is already waiting to be sent.");
        delivery.Status = ReportEmailStatus.Pending;
        delivery.Attempts = 0;
        delivery.NextAttemptAt = null;
        delivery.LastError = null;
        delivery.UpdatedAtUtc = clock.UtcNow;
        delivery.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(cancellationToken);
        wakeUp.Signal();
        return Map(delivery);
    }

    private async Task ApplyAsync(ReportDistributionRule rule, SaveReportDistributionRuleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength) throw new ReportDistributionValidationException($"A rule name of up to {MaxNameLength} characters is required.");
        if (await db.ReportDistributionRules.AnyAsync(item => item.Name == name && item.Id != rule.Id, cancellationToken)) throw new ReportDistributionValidationException($"A rule named \"{name}\" already exists.");

        var factories = Normalize(request.FactoryCodes);
        var categories = Normalize(request.CategoryCodes);
        var knownFactories = await db.Factories.Where(item => factories.Contains(item.Code)).Select(item => item.Code).ToListAsync(cancellationToken);
        var knownCategories = await db.EmployeeCategories.Where(item => categories.Contains(item.Code)).Select(item => item.Code).ToListAsync(cancellationToken);
        if (factories.Except(knownFactories).FirstOrDefault() is { } unknownFactory) throw new ReportDistributionValidationException($"Factory '{unknownFactory}' was not found.");
        if (categories.Except(knownCategories).FirstOrDefault() is { } unknownCategory) throw new ReportDistributionValidationException($"Category '{unknownCategory}' was not found.");
        if (string.Join(",", factories).Length > 1000 || string.Join(",", categories).Length > 1000) throw new ReportDistributionValidationException("Too many factories or categories for one rule.");

        var userIds = (request.UserIds ?? []).Distinct().ToList();
        var activeUsers = await db.Users.Where(user => userIds.Contains(user.Id) && user.Status == UserAccountStatus.Active).Select(user => new { user.Id, user.Email }).ToListAsync(cancellationToken);
        // A user already on the rule stays even if their account has since been deactivated (they are skipped when sending).
        var keptUsers = rule.Recipients.Where(recipient => recipient.UserId is { } id && userIds.Contains(id)).Select(recipient => recipient.UserId!.Value).ToHashSet();
        if (userIds.Any(id => activeUsers.All(user => user.Id != id) && !keptUsers.Contains(id)))
            throw new ReportDistributionValidationException("A chosen user was not found or is not active.");
        var userEmails = activeUsers.Select(user => user.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = new List<string>();
        foreach (var raw in request.Emails ?? [])
        {
            var email = raw?.Trim() ?? string.Empty;
            if (email.Length == 0) continue;
            if (email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out var address) || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
                throw new ReportDistributionValidationException($"'{email}' is not a valid email address.");
            // A typed address that belongs to a chosen user would get the report twice.
            if (!userEmails.Contains(email) && !emails.Contains(email, StringComparer.OrdinalIgnoreCase)) emails.Add(email);
        }
        if (userIds.Count == 0 && emails.Count == 0) throw new ReportDistributionValidationException("Add at least one recipient: a system user or an email address.");

        rule.Name = name;
        rule.FactoryCodes = string.Join(",", factories);
        rule.CategoryCodes = string.Join(",", categories);
        rule.IsActive = request.IsActive;
        foreach (var old in rule.Recipients.ToList()) { rule.Recipients.Remove(old); db.ReportDistributionRecipients.Remove(old); }
        foreach (var id in userIds) rule.Recipients.Add(new ReportDistributionRecipient { RuleId = rule.Id, UserId = id });
        foreach (var email in emails) rule.Recipients.Add(new ReportDistributionRecipient { RuleId = rule.Id, Email = email });
    }

    private async Task<ReportDistributionRuleDto> GetRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await RulesWithRecipients().SingleAsync(item => item.Id == id, cancellationToken);
        return Map(rule, await MasterNamesAsync(cancellationToken));
    }

    private IQueryable<ReportDistributionRule> RulesWithRecipients() =>
        db.ReportDistributionRules.AsNoTracking().Include(rule => rule.Recipients).ThenInclude(recipient => recipient.User);

    private async Task<(Dictionary<string, string> Factories, Dictionary<string, string> Categories)> MasterNamesAsync(CancellationToken cancellationToken) =>
        (await db.Factories.AsNoTracking().ToDictionaryAsync(item => item.Code, item => item.Name, cancellationToken),
         await db.EmployeeCategories.AsNoTracking().ToDictionaryAsync(item => item.Code, item => item.Name, cancellationToken));

    private static ReportDistributionRuleDto Map(ReportDistributionRule rule, (Dictionary<string, string> Factories, Dictionary<string, string> Categories) names)
    {
        var factories = Codes(rule.FactoryCodes);
        var categories = Codes(rule.CategoryCodes);
        var recipients = rule.Recipients
            .Select(recipient => recipient.User is { } user
                ? new ReportRecipientDto(user.Id, user.Email, user.DisplayName, user.Status != UserAccountStatus.Active)
                : new ReportRecipientDto(null, recipient.Email ?? string.Empty, null, false))
            .OrderBy(recipient => recipient.UserId is null).ThenBy(recipient => recipient.DisplayName ?? recipient.Email)
            .ToList();
        return new ReportDistributionRuleDto(rule.Id, rule.Name, factories, Label(factories, names.Factories, "All factories"), categories, Label(categories, names.Categories, "All categories"), recipients, rule.IsActive);
    }

    private static ReportEmailDeliveryDto Map(ReportEmailDelivery delivery) =>
        new(delivery.Id, delivery.RuleId, delivery.RuleName, delivery.Recipients, delivery.Status, delivery.Attempts, delivery.CreatedAtUtc, delivery.RequestedByName, delivery.SentAt, delivery.LastError, delivery.BillBatchId);

    // Active users' current addresses (by name), then the typed addresses, without duplicates.
    private static List<string> ActiveRecipients(ReportDistributionRule rule) =>
        rule.Recipients
            .OrderBy(recipient => recipient.User is null).ThenBy(recipient => recipient.User?.DisplayName ?? recipient.Email, StringComparer.OrdinalIgnoreCase)
            .Select(recipient => recipient.User is { } user ? (user.Status == UserAccountStatus.Active ? user.Email : null) : recipient.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static List<string> Codes(string codes) => codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static List<string> Normalize(IReadOnlyList<string>? codes) =>
        (codes ?? []).Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()).Distinct(StringComparer.Ordinal).ToList();

    private static string Label(IReadOnlyList<string> codes, IReadOnlyDictionary<string, string> names, string all) =>
        codes.Count == 0 ? all : string.Join(", ", codes.Select(code => names.TryGetValue(code, out var name) ? name : code));
}

// Wakes the background sender as soon as report emails are queued, instead of at its next check.
public sealed class ReportEmailWakeUp
{
    private readonly SemaphoreSlim signal = new(0, 1);

    public void Signal() { if (signal.CurrentCount == 0) { try { signal.Release(); } catch (SemaphoreFullException) { } } }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => await signal.WaitAsync(timeout, cancellationToken);
}
