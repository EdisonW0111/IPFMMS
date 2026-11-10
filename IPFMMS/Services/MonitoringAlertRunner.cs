using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Services;

// Each recipient gets a fresh scope so a failed transaction cannot affect another account.
public sealed class MonitoringAlertRunner(IServiceScopeFactory scopes, ILogger<MonitoringAlertRunner> logger)
{
    private const int BatchSize = 20;

    public async Task RunAsync(DateTime today, CancellationToken ct = default)
    {
        today = today.Date;
        string? after = null;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            List<string> recipients;
            await using (var batch = scopes.CreateAsyncScope())
            {
                var db = batch.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                recipients = await db.Users.AsNoTracking()
                    .Where(u => u.AccountStatus == AccountStatus.Active && (u.Role == "User" || u.Role == "Parent")
                        && (after == null || string.Compare(u.UserId, after) > 0)
                        && db.ParentStudentLinks.Any(l => l.parentId == u.UserId
                            && l.linkStatus == "Active" && l.acceptedDate != null
                            && l.parentHiddenDate == null && l.studentHiddenDate == null
                            && l.Student!.AccountStatus == AccountStatus.Active
                            && (l.Student.Role == "User" || l.Student.Role == "Parent")))
                    .OrderBy(u => u.UserId).Select(u => u.UserId).Take(BatchSize).ToListAsync(ct);
            }
            if (recipients.Count == 0) return;
            // Keyset paging stays stable when links/accounts change during the scan.
            after = recipients[^1];
            foreach (var recipient in recipients)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<MonitoringAlertService>()
                        .RefreshAsync(recipient, today, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Monitoring alert check failed for recipient {UserId}; the next scan will retry.", recipient);
                }
            }
        }
    }
}
