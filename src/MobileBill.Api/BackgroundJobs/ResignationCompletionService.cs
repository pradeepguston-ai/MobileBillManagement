using MobileBill.Application.MasterData;

namespace MobileBill.Api.BackgroundJobs;

// Completes pending resignations on their date: at start-up and then every hour, so an employee becomes
// inactive and their numbers go to the SIM Pool on the day they leave, before the month's bills are matched.
public sealed class ResignationCompletionService(IServiceScopeFactory scopes, ILogger<ResignationCompletionService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var completed = await scope.ServiceProvider.GetRequiredService<IMasterDataService>().CompleteDueResignationsAsync(stoppingToken);
                if (completed > 0) logger.LogInformation("Completed {Count} pending resignation(s).", completed);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Completing pending resignations failed; it will be retried in an hour.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
