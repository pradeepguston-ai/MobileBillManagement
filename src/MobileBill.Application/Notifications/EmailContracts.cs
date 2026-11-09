namespace MobileBill.Application.Notifications;

public sealed record EmailMessage(IReadOnlyList<string> To, string Subject, string HtmlBody, IReadOnlyList<EmailAttachment>? Attachments = null);
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

// Sends one message now. Throws EmailDeliveryException when it cannot.
public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

// Messages wait here and are sent in the background, so a slow or failing mail service never holds up the request
// that caused the email.
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
    IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken);
}

// Tells the next approver (Head of IT, Group HR Manager or CFO) that a billing batch is waiting for them.
// Never throws: a notification that cannot be prepared is logged and the workflow carries on.
public interface IBillApprovalNotifier
{
    Task NotifyAwaitingApprovalAsync(Guid batchId, CancellationToken cancellationToken);
}

// Transient: the mail service was busy or unreachable and the message can be sent again later.
public sealed class EmailDeliveryException(string message, bool isTransient, TimeSpan? retryAfter = null, Exception? inner = null) : Exception(message, inner)
{
    public bool IsTransient { get; } = isTransient;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
