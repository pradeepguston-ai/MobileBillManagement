using Microsoft.Extensions.Options;
using MobileBill.Application.Reports;
using MobileBill.Infrastructure.Notifications;
using MobileBill.Infrastructure.Reports;

namespace MobileBill.Api.BackgroundJobs;

// Works through the queued report emails (kept in the database, so a restart loses nothing), one at a time and spaced out
// for Microsoft 365's sending limit. Woken as soon as emails are queued; otherwise checks every 30 seconds for retries that are due.
public sealed class ReportEmailDispatchService(IServiceScopeFactory scopes, ReportEmailWakeUp wakeUp, IOptions<EmailOptions> options, ILogger<ReportEmailDispatchService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleCheck = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var spacing = TimeSpan.FromSeconds(Math.Max(0, options.Value.SecondsBetweenMessages));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool sent;
                await using (var scope = scopes.CreateAsyncScope())
                    sent = await scope.ServiceProvider.GetRequiredService<IReportEmailProcessor>().ProcessNextAsync(stoppingToken);
                if (sent) await Task.Delay(spacing, stoppingToken);
                else await wakeUp.WaitAsync(IdleCheck, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;   // shutting down
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Sending report emails failed; trying again shortly.");
                try { await Task.Delay(IdleCheck, stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
