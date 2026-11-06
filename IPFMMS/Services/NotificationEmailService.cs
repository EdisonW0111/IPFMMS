using IPFMMS.Data;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class NotificationEmailService(ApplicationDbContext db, NotificationService notifications,
    IAlertEmailSender mail, ILogger<NotificationEmailService> logger)
{
    public async Task DeliverPendingAsync(string owner, CancellationToken ct = default)
    {
        // Small batches keep interactive refreshes responsive. A later check continues the queue.
        var ids = await notifications.Visible(owner).Where(n => n.emailStatus == "Pending")
            .OrderBy(n => n.createdDateTime).ThenBy(n => n.notificationId).Select(n => n.notificationId).Take(3).ToListAsync(ct);
        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) return;
            // Once claimed, finish delivery bookkeeping even if the AJAX request is cancelled.
            var deliveryCt = CancellationToken.None;
            // Persist the claim before SMTP; another tab/server cannot claim it again.
            if (await notifications.Visible(owner).Where(n => n.notificationId == id && n.emailStatus == "Pending")
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Sending").SetProperty(n => n.emailAttemptedDate, DateTime.UtcNow), deliveryCt) == 0) continue;
            var pair = await db.Notifications.AsNoTracking().Where(n => n.notificationId == id && n.userId == owner)
                .Select(n => new { n.relatedUserId }).SingleAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, deliveryCt);
            // Consent cannot be revoked midway through the send: revocation uses the same account locks.
            await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` IN ({owner}, {pair.relatedUserId}) ORDER BY `UserId` FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            var notification = await notifications.Visible(owner).SingleOrDefaultAsync(n => n.notificationId == id && n.emailStatus == "Sending", deliveryCt);
            if (notification == null)
            {
                await db.Notifications.Where(n => n.notificationId == id && n.userId == owner && n.emailStatus == "Sending")
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Suppressed"), deliveryCt);
                await tx.CommitAsync(deliveryCt); continue;
            }
            var email = await db.Users.Where(u => u.UserId == owner).Select(u => u.Email).SingleAsync(ct);
            var status = "Sent";
            try { await mail.SendAsync(email, notification.title, notification.message); }
            catch (Exception)
            {
                status = "Failed";
                logger.LogWarning("Alert email failed for notification {NotificationId}. In-app notification retained; user may retry.", id);
            }
            await db.Notifications.Where(n => n.notificationId == id && n.emailStatus == "Sending")
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, status).SetProperty(n => n.emailSentDate, status == "Sent" ? DateTime.UtcNow : (DateTime?)null), deliveryCt);
            await tx.CommitAsync(deliveryCt);
        }
    }

    public async Task<bool> QueueRetryAsync(string owner, string id, CancellationToken ct = default)
    {
        var updated = await notifications.Visible(owner).Where(n => n.notificationId == id && (n.emailStatus == "Failed" || n.emailStatus == "NotRequested")
            && (n.emailAttemptedDate == null || n.emailAttemptedDate < DateTime.UtcNow.AddMinutes(-1)))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Pending"), ct);
        if (updated == 0) return false;
        await DeliverPendingAsync(owner, ct);
        return true;
    }
}
