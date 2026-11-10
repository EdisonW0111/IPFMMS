namespace IPFMMS.Services;

public sealed class MonitoringAlertWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<MonitoringAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            // Enabled by default; an explicit false disables background monitoring checks.
            if (!(config.GetValue<bool?>("MonitoringAlerts:Enabled") ?? true)) return;
            try
            {
                var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MonitoringAlertRunner>()
                    .RunAsync(today, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Monitoring alert scan failed; the next scan will retry.");
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
