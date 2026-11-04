namespace IPFMMS.Services;

public sealed class AutomaticSavingsWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<AutomaticSavingsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Explicit opt-in: apply the migration before enabling this worker.
        if (!config.GetValue<bool>("AutomaticSavings:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AutomaticSavingsService>()
                    .ProcessDueAsync(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Automatic savings processing failed. Uncommitted attempts will retry on the next run.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
