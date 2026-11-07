using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class NotificationService(ApplicationDbContext db)
{
    // Financial notifications remain visible only during the same consented request version.
    public IQueryable<Notification> Visible(string owner)
    {
        var today=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
        return db.Notifications.AsNoTracking().Where(n => n.userId == owner
        && (n.routineBillId == null || db.RoutineBills.Any(b => b.billId == n.routineBillId && b.userId == owner && b.status == "Active" && b.dueDate == n.billDueDate
            && b.User!.AccountStatus == AccountStatus.Active && (b.User.Role == "User" || b.User.Role == "Parent")
            && !db.RoutineBillPayments.Any(p => p.billId == b.billId && p.dueDate == b.dueDate)
            && ((n.notificationType == "BillUpcoming" && b.dueDate > today && b.dueDate <= today.AddDays(b.reminderPeriod))
                || (n.notificationType == "BillDueToday" && b.dueDate == today)
                || (n.notificationType == "BillOverdue" && b.dueDate < today))))
        && (n.linkId == null || (n.Link != null && n.Link.parentId == owner && n.Link.userId == n.relatedUserId
            && n.Link.linkStatus == "Active" && n.Link.acceptedDate != null && n.Link.parentHiddenDate == null && n.Link.studentHiddenDate == null && n.Link.requestVersion == n.requestVersion
            && n.Link.Parent!.AccountStatus == AccountStatus.Active && n.Link.Student!.AccountStatus == AccountStatus.Active
            && (n.Link.Parent.Role == "User" || n.Link.Parent.Role == "Parent") && (n.Link.Student.Role == "User" || n.Link.Student.Role == "Parent"))));
    }
    public Task<int> UnreadAsync(string owner, CancellationToken ct = default) => Visible(owner).CountAsync(n => !n.isRead, ct);
    public async Task<bool> SetReadAsync(string owner, string id, bool read, CancellationToken ct = default) =>
        await Visible(owner).Where(n => n.notificationId == id).ExecuteUpdateAsync(s => s.SetProperty(n => n.isRead, read), ct) > 0;
    public Task<int> MarkAllReadAsync(string owner, CancellationToken ct = default) =>
        Visible(owner).Where(n => !n.isRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.isRead, true), ct);
}
