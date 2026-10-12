using MobileBill.Application.MasterData;

namespace MobileBill.Api.BackgroundJobs;

// Completes pending resignations on their date: once the API has started and then every hour, so an employee becomes
// inactive and their numbers go to the SIM Pool on the day they leave, before the month's bills are matched.
public sealed class ResignationCompletionService(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, ILogger<ResignationCompletionService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Waits for start-up to finish: if it fails (for example the port is already in use) the host shuts down
        // and this never runs against services that are being disposed.
        if (!await StartedAsync(stoppingToken)) return;

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var completed = await scope.ServiceProvider.GetRequiredService<IMasterDataService>().CompleteDueResignationsAsync(stoppingToken);
                if (completed > 0) logger.LogInformation("Completed {Count} pending resignation(s).", completed);
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                return;   // shutting down
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Completing pending resignations failed; it will be retried in an hour.");
            }
        }
        while (await NextTickAsync(timer, stoppingToken));
    }

    private async Task<bool> StartedAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var onStarted = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        using var onStopping = stoppingToken.Register(() => started.TrySetCanceled());
        try { await started.Task; return true; }
        catch (OperationCanceledException) { return false; }
    }

    private static async Task<bool> NextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try { return await timer.WaitForNextTickAsync(stoppingToken); }
        catch (OperationCanceledException) { return false; }
    }
}
