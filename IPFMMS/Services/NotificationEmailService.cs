using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class NotificationEmailService(ApplicationDbContext db, NotificationService notifications,
    IAlertEmailSender mail, ILogger<NotificationEmailService> logger)
{
    private static bool EmailEnabled(UserPreference preferences, string type) => type switch
    {
        "BillUpcoming" or "BillDueToday" or "BillOverdue" => preferences.BillEmail,
        "BudgetOverspending" or "LowFinancialHealth" => preferences.MonitoringEmail,
        _ => true
    };
    private async Task<UserPreference> Preferences(string owner, CancellationToken ct) =>
        await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == owner, ct) ?? new UserPreference();

    public async Task DeliverPendingAsync(string owner, CancellationToken ct = default)
    {
        // Email-only alerts remain eligible even when no in-app item was requested.
        var ids = await notifications.Eligible(owner).Where(n => n.emailStatus == "Pending")
            .OrderBy(n => n.createdDateTime).ThenBy(n => n.notificationId).Select(n => n.notificationId).Take(3).ToListAsync(ct);
        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) return;
            var pending = await notifications.Eligible(owner).SingleOrDefaultAsync(n => n.notificationId == id && n.emailStatus == "Pending", ct);
            if (pending == null) continue;
            if (!EmailEnabled(await Preferences(owner, ct), pending.notificationType))
            {
                await db.Notifications.Where(n => n.notificationId == id && n.userId == owner && n.emailStatus == "Pending")
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Disabled"), ct);
                continue;
            }
            // Once claimed, finish delivery bookkeeping even if the browser cancels its request.
            var deliveryCt = CancellationToken.None;
            if (await notifications.Eligible(owner).Where(n => n.notificationId == id && n.emailStatus == "Pending")
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Sending").SetProperty(n => n.emailAttemptedDate, DateTime.UtcNow), deliveryCt) == 0) continue;
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, deliveryCt);
            // Consent changes and settings saves take these same account locks.
            await db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE UserId IN ({owner}, {pending.relatedUserId}) ORDER BY UserId FOR UPDATE")
                .AsNoTracking().ToListAsync(deliveryCt);
            var notification = await notifications.Eligible(owner).SingleOrDefaultAsync(n => n.notificationId == id && n.emailStatus == "Sending", deliveryCt);
            if (notification == null)
            {
                await db.Notifications.Where(n => n.notificationId == id && n.userId == owner && n.emailStatus == "Sending")
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Suppressed"), deliveryCt);
                await tx.CommitAsync(deliveryCt); continue;
            }
            // Recheck after locking so an email preference cannot be changed midway through SMTP.
            if (!EmailEnabled(await Preferences(owner, deliveryCt), notification.notificationType))
            {
                await db.Notifications.Where(n => n.notificationId == id && n.userId == owner && n.emailStatus == "Sending")
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Disabled"), deliveryCt);
                await tx.CommitAsync(deliveryCt); continue;
            }
            var email = await db.Users.Where(u => u.UserId == owner).Select(u => u.Email).SingleAsync(deliveryCt);
            var status = "Sent";
            try { await mail.SendAsync(email, notification.title, notification.message); }
            catch (Exception)
            {
                status = "Failed";
                logger.LogWarning("Alert email failed for notification {NotificationId}. The alert is retained; user may retry.", id);
            }
            await db.Notifications.Where(n => n.notificationId == id && n.emailStatus == "Sending")
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, status).SetProperty(n => n.emailSentDate, status == "Sent" ? DateTime.UtcNow : (DateTime?)null), deliveryCt);
            await tx.CommitAsync(deliveryCt);
        }
    }

    public async Task<bool> QueueRetryAsync(string owner, string id, CancellationToken ct = default)
    {
        var related = await db.Notifications.AsNoTracking().Where(n => n.notificationId == id && n.userId == owner)
            .Select(n => new { n.relatedUserId }).SingleOrDefaultAsync(ct);
        if (related == null) return false;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            await db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE UserId IN ({owner}, {related.relatedUserId}) ORDER BY UserId FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            var notification = await notifications.Visible(owner).SingleOrDefaultAsync(n => n.notificationId == id
                && (n.emailStatus == "Failed" || n.emailStatus == "NotRequested")
                && (n.emailAttemptedDate == null || n.emailAttemptedDate < DateTime.UtcNow.AddMinutes(-1)), ct);
            if (notification == null) return false;
            if (!EmailEnabled(await Preferences(owner, ct), notification.notificationType))
            {
                await db.Notifications.Where(n => n.notificationId == id && n.userId == owner
                    && (n.emailStatus == "Failed" || n.emailStatus == "NotRequested"))
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Disabled"), ct);
                await tx.CommitAsync(ct); return false;
            }
            await db.Notifications.Where(n => n.notificationId == id && n.userId == owner
                && (n.emailStatus == "Failed" || n.emailStatus == "NotRequested"))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Pending"), ct);
            await tx.CommitAsync(ct);
        }
        await DeliverPendingAsync(owner, ct);
        return true;
    }
}
