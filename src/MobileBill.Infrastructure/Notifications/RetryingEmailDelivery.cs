using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobileBill.Application.Notifications;

namespace MobileBill.Infrastructure.Notifications;

// Sends a message, trying again after a delay when the mail service is busy or unreachable. A refusal that will not
// change (wrong credentials, a bad address) is given up at once. Returns whether the message was sent.
public sealed class RetryingEmailDelivery(IEmailSender sender, IOptions<EmailOptions> options, ILogger<RetryingEmailDelivery> logger)
{
    private readonly EmailOptions settings = options.Value;

    public async Task<bool> DeliverAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, settings.MaxAttempts);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await sender.SendAsync(message, cancellationToken);
                logger.LogInformation("Sent \"{Subject}\" to {Count} recipient(s).", message.Subject, message.To.Count);
                return true;
            }
            catch (EmailDeliveryException exception) when (exception.IsTransient && attempt < attempts)
            {
                // The service's own Retry-After when it gives one, otherwise the configured delay, longer each time.
                var delay = exception.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(Math.Max(0, settings.RetryDelaySeconds) * attempt);
                logger.LogWarning("Sending \"{Subject}\" failed (attempt {Attempt} of {Attempts}): {Reason} Trying again in {Delay}.", message.Subject, attempt, attempts, exception.Message, delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (EmailDeliveryException exception)
            {
                logger.LogError("Gave up sending \"{Subject}\" after {Attempt} attempt(s): {Reason}", message.Subject, attempt, exception.Message);
                return false;
            }
        }
    }
}
