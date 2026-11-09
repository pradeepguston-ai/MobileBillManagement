using Microsoft.Extensions.Options;
using MobileBill.Application.Notifications;
using MobileBill.Infrastructure.Notifications;

namespace MobileBill.Api.BackgroundJobs;

// Sends queued emails one at a time, spaced out to stay under Microsoft 365's sending limit. Retries are handled by
// RetryingEmailDelivery; a message that still cannot be sent is logged and the next one goes ahead.
public sealed class EmailDispatchService(IEmailQueue queue, IServiceScopeFactory scopes, IOptions<EmailOptions> options, ILogger<EmailDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var spacing = TimeSpan.FromSeconds(Math.Max(0, options.Value.SecondsBetweenMessages));
        try
        {
            await foreach (var message in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<RetryingEmailDelivery>().DeliverAsync(message, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Sending \"{Subject}\" failed.", message.Subject);
                }
                await Task.Delay(spacing, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }
}
