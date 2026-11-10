using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;

public static class AdminGuard
{
    public static async Task RequireAsync(ApplicationDbContext db, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || !await db.Users.AsNoTracking().AnyAsync(u => u.UserId == actor
            && u.Role == "Admin" && u.AccountStatus == AccountStatus.Active, ct))
            throw new UnauthorizedAccessException();
    }

    public static async Task LockAsync(ApplicationDbContext db, string actor, CancellationToken ct = default,
        IEnumerable<string>? targetUserIds = null)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("An administrator transaction is required.");
        // Lock the administrator AND affected accounts together in the same global ID order
        // as monitoring/authentication. The acting administrator stays active and locked until
        // commit; self-removal is separately forbidden, so concurrent changes retain an admin.
        var targets = (targetUserIds ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (targets.Length > 2) throw new ArgumentException("An Admin operation supports at most two affected accounts.");
        var first = targets.ElementAtOrDefault(0) ?? actor;
        var second = targets.ElementAtOrDefault(1) ?? actor;
        var accounts = await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` IN ({actor}, {first}, {second}) ORDER BY `UserId` FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        if (!accounts.Any(u => u.UserId == actor && u.Role == "Admin" && u.AccountStatus == AccountStatus.Active))
            throw new UnauthorizedAccessException();
    }

    public static async Task AuditAsync(ApplicationDbContext db, IdGeneratorService ids, string actor,
        string entityType, string entityId, string action, string summary, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Management history requires a transaction.");
        ct.ThrowIfCancellationRequested();
        db.AdminAuditEntries.Add(new AdminAuditEntry {
            AuditId = await ids.GenerateIdAsync("AUD"), AdminUserId = actor, EntityType = entityType,
            EntityId = entityId, Action = action, Summary = summary, CreatedDate = DateTime.UtcNow
        });
    }
}
