namespace MobileBill.Infrastructure.Notifications;

// Settings come from configuration ("Email" section). TenantId, ClientId and ClientSecret are confidential: they belong in
// user secrets or the Email__TenantId / Email__ClientId / Email__ClientSecret environment variables, never in a committed file.
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    // Nothing is sent until this is true and the three credentials are set.
    public bool Enabled { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    // The only mailbox the app registration may send from.
    public string SenderMailbox { get; set; } = "noreply.mobilebill@concord.lk";
    public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0/";
    public string LoginBaseUrl { get; set; } = "https://login.microsoftonline.com/";
    // The web app's address, for the link to the batch in the email, for example https://mobilebill.concord.lk.
    public string? AppBaseUrl { get; set; }
    // While testing: every email goes to this internal address instead of the real approvers.
    public string? TestRecipient { get; set; }
    // A failed send is tried again after RetryDelaySeconds, then twice that, and so on, up to MaxAttempts in all.
    public int MaxAttempts { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 60;
    // Spacing between messages, to stay well under Microsoft 365's 30 messages a minute.
    public int SecondsBetweenMessages { get; set; } = 3;
    // Email the monthly bill report to every active Report Distribution rule when a batch is locked.
    public bool SendReportsOnLock { get; set; } = true;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
