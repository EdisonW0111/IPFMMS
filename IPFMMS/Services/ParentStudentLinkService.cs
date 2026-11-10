using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;

public sealed record AdminLinkCreateResult(string LinkId, InvitationEmailDelivery Delivery);

public sealed class ParentStudentLinkService(ApplicationDbContext db, IdGeneratorService ids, MonitoringLinkService monitoring)
{
    private static bool Eligible(User u) =>
        u.AccountStatus == AccountStatus.Active && (u.Role == "User" || u.Role == "Parent");

    public static string Stamp(ParentStudentLink link)
    {
        var raw = string.Join("|", link.linkId, link.parentId, link.userId, link.linkStatus,
            link.requestVersion.ToString(CultureInfo.InvariantCulture),
            link.acceptedDate?.ToString("O", CultureInfo.InvariantCulture) ?? "",
            link.parentHiddenDate?.ToString("O", CultureInfo.InvariantCulture) ?? "",
            link.studentHiddenDate?.ToString("O", CultureInfo.InvariantCulture) ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private Task<List<User>> LockAccountsAsync(string parentId, string studentId, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE UserId IN ({parentId}, {studentId}) ORDER BY UserId FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);

    public async Task<AdminLinkCreateResult> CreateAsync(string actor, string parentId, string studentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(studentId)
            || parentId == studentId || parentId.Length > 20 || studentId.Length > 20)
            throw new InvalidOperationException("Select two different valid accounts.");
        string id;
        int version;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            // Lock actor and participants in the same global account order as user-side requests.
            await AdminGuard.LockAsync(db, actor, ct, new[] { parentId, studentId });
            var accounts = await LockAccountsAsync(parentId, studentId, ct);
            if (accounts.Count != 2 || !accounts.All(Eligible))
                throw new InvalidOperationException("Select two different active user accounts. Administrator accounts cannot participate.");
            // Resolve canonical stored IDs using the database's own collation while
            // the pair is locked; caller casing/accent aliases are never persisted.
            parentId = await db.Users.AsNoTracking().Where(u => u.UserId == parentId).Select(u => u.UserId).SingleAsync(ct);
            studentId = await db.Users.AsNoTracking().Where(u => u.UserId == studentId).Select(u => u.UserId).SingleAsync(ct);
            if (parentId == studentId) throw new InvalidOperationException("Select two different valid accounts.");
            var link = await db.ParentStudentLinks.SingleOrDefaultAsync(l => l.parentId == parentId && l.userId == studentId, ct);
            if (link != null) await db.Entry(link).ReloadAsync(ct);
            if (link?.linkStatus == "Pending")
                throw new InvalidOperationException("These accounts already have a pending request. The recipient must review it.");
            if (link?.linkStatus == "Active" && link.acceptedDate.HasValue)
                throw new InvalidOperationException("These accounts already have an accepted monitoring relationship.");
            var now = DateTime.UtcNow;
            if (link?.requestedDate > now.AddMinutes(-1))
                throw new InvalidOperationException("Wait one minute before creating another request for these accounts.");
            if (link == null)
            {
                link = new ParentStudentLink
                {
                    linkId = await ids.GenerateIdAsync("PSL"), parentId = parentId,
                    userId = studentId, linkedDate = now
                };
                db.ParentStudentLinks.Add(link);
            }
            // Even a retained legacy/inactive record needs a fresh recipient decision.
            link.parentId = parentId;
            link.userId = studentId;
            link.linkStatus = "Pending";
            link.requestVersion++;
            link.requestedDate = now;
            link.acceptedDate = null;
            link.parentHiddenDate = null;
            link.studentHiddenDate = null;
            link.emailSentDate = null;
            link.lastEmailAttemptDate = null;
            id = link.linkId;
            version = link.requestVersion;
            await AdminGuard.AuditAsync(db, ids, actor, "ParentStudentLink", id, "CreatePending",
                $"Created a pending monitoring request between {parentId} and {studentId}; recipient acceptance is required.", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.Entry(link).State = EntityState.Detached;
        }
        InvitationEmailDelivery delivery;
        try
        {
            // Recipient preferences, current account status and pending consent are checked again.
            delivery = await monitoring.ResendAsync(parentId, id, version, CancellationToken.None)
                ?? InvitationEmailDelivery.Suppressed;
        }
        catch (InvalidOperationException)
        {
            // A concurrent response/cancellation can make the committed invitation unavailable.
            delivery = InvitationEmailDelivery.Suppressed;
        }
        return new(id, delivery);
    }

    public Task<bool> CancelAsync(string actor, string id, int version, string? stamp, CancellationToken ct = default) =>
        ChangeAsync(actor, id, version, stamp, cancel: true, ct);

    public Task<bool> DeactivateAsync(string actor, string id, int version, string? stamp, CancellationToken ct = default) =>
        ChangeAsync(actor, id, version, stamp, cancel: false, ct);

    private async Task<bool> ChangeAsync(string actor, string id, int version, string? stamp, bool cancel, CancellationToken ct)
    {
        var pair = await db.ParentStudentLinks.AsNoTracking().Where(l => l.linkId == id)
            .Select(l => new { l.parentId, l.userId }).SingleOrDefaultAsync(ct);
        if (pair == null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AdminGuard.LockAsync(db, actor, ct, new[] { pair.parentId, pair.userId });
        await LockAccountsAsync(pair.parentId, pair.userId, ct);
        var link = await db.ParentStudentLinks.SingleOrDefaultAsync(l => l.linkId == id, ct);
        if (link == null) return false;
        await db.Entry(link).ReloadAsync(ct);
        if (link.parentId != pair.parentId || link.userId != pair.userId
            || link.requestVersion != version || !string.Equals(Stamp(link), stamp, StringComparison.Ordinal))
            throw new InvalidOperationException("This relationship changed. Review its current details before confirming.");
        if (cancel ? link.linkStatus != "Pending" : link.linkStatus != "Active")
            throw new InvalidOperationException(cancel ? "Only a pending request can be cancelled." : "Only an active relationship can be deactivated.");
        link.linkStatus = cancel ? "Cancelled" : "Inactive";
        link.acceptedDate = null;
        await AdminGuard.AuditAsync(db, ids, actor, "ParentStudentLink", id, cancel ? "Cancel" : "Deactivate",
            cancel ? "Cancelled the pending monitoring request; no access was granted."
                : "Deactivated monitoring access; relationship history was retained and a new request requires fresh acceptance.", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
