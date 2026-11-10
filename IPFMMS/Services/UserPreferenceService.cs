using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Services;

public sealed class UserPreferenceService(ApplicationDbContext db)
{
    public async Task<UserPreference> GetAsync(string owner, CancellationToken ct = default) =>
        await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == owner, ct)
        ?? new UserPreference { UserId = owner, Revision = 0 };

    public async Task<string> ThemeAsync(ClaimsPrincipal user, string? cookie, CancellationToken ct = default)
    {
        var owner = user.FindFirstValue(ClaimTypes.NameIdentifier);
        // Signed-in accounts use their own saved value, never another account's browser preference.
        if (user.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(owner)
            && (user.IsInRole("User") || user.IsInRole("Parent") || user.IsInRole("Admin")))
        {
            var preference = await GetAsync(owner, ct);
            return preference.Theme == "Dark" ? "Dark" : "Light";
        }
        return cookie == "Dark" ? "Dark" : "Light";
    }

    public async Task SaveAsync(string owner, SettingsViewModel model, CancellationToken ct = default)
    {
        if (model.Theme is not ("Light" or "Dark"))
            throw new InvalidOperationException("Choose Light or Dark mode.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        // Delivery services use the same account lock before sending, so an opt-out is respected.
        var users = await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        if (!users.Any(u => u.AccountStatus == AccountStatus.Active && (u.Role == "User" || u.Role == "Parent" || u.Role == "Admin")))
            throw new UnauthorizedAccessException();
        var preference = await db.UserPreferences.SingleOrDefaultAsync(p => p.UserId == owner, ct);
        if (preference != null) await db.Entry(preference).ReloadAsync(ct);
        if ((preference?.Revision ?? 0) != model.Revision)
            throw new InvalidOperationException("Your settings changed in another tab. Reload this page before saving again.");
        if (preference == null)
        {
            preference = new UserPreference { UserId = owner, Revision = 0 };
            db.UserPreferences.Add(preference);
        }
        preference.Theme = model.Theme;
        preference.BillInApp = model.BillInApp;
        preference.BillEmail = model.BillEmail;
        preference.MonitoringInApp = model.MonitoringInApp;
        preference.MonitoringEmail = model.MonitoringEmail;
        preference.InvitationEmail = model.InvitationEmail;
        preference.Revision++;
        await db.SaveChangesAsync(ct);
        // Do not send an existing queued alert after its recipient turns that email category off.
        // Re-enabling applies to future events; it does not release an old email backlog.
        var pending = db.Notifications.Where(n => n.userId == owner
            && (n.emailStatus == "Pending" || n.emailStatus == "Failed" || n.emailStatus == "NotRequested"));
        if (!model.BillEmail)
            await pending.Where(n => n.notificationType == "BillUpcoming" || n.notificationType == "BillDueToday" || n.notificationType == "BillOverdue")
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Disabled"), ct);
        if (!model.MonitoringEmail)
            await pending.Where(n => n.notificationType == "BudgetOverspending" || n.notificationType == "LowFinancialHealth")
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.emailStatus, "Disabled"), ct);
        await tx.CommitAsync(ct);
    }
}
