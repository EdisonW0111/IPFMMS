using IPFMMS.Data;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class PersonalNotificationWorker(IServiceScopeFactory scopes, ILogger<PersonalNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do {
            try {
                string? cursor = null;
                while (!ct.IsCancellationRequested) {
                    using var batch = scopes.CreateScope();
                    var db = batch.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var owners = await db.Users.AsNoTracking().Where(u => u.AccountStatus == "Active" && (u.Role == "User" || u.Role == "Parent") && (cursor == null || u.UserId.CompareTo(cursor) > 0))
                        .OrderBy(u => u.UserId).Select(u => u.UserId).Take(20).ToListAsync(ct);
                    if (owners.Count == 0) break;
                    foreach (var owner in owners) {
                        try {
                            using var scope = scopes.CreateScope();
                            await scope.ServiceProvider.GetRequiredService<PersonalNotificationService>().RefreshAsync(owner, DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date, ct);

                        } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                        catch (Exception ex) { logger.LogError(ex, "Personal notification check failed; a later check will retry."); }
                        try {
                            using var delivery = scopes.CreateScope();
                            await delivery.ServiceProvider.GetRequiredService<NotificationEmailService>().DeliverPendingAsync(owner, ct);
                        } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                        catch (Exception ex) { logger.LogError(ex, "Notification email delivery failed; a later check will retry queued messages."); }
                    }
                    cursor = owners[^1];
                }
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Personal notification worker failed; a later check will retry."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
