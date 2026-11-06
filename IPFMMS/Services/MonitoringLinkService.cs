using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class MonitoringLinkService(ApplicationDbContext db, IdGeneratorService ids,
    IParentLinkEmailSender mail, ILogger<MonitoringLinkService> logger)
{
    private static bool Eligible(User u) => u.AccountStatus == AccountStatus.Active && (u.Role == "User" || u.Role == "Parent");
    private Task<List<User>> LockUsers(string a, string b, CancellationToken ct) => db.Users
        .FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` IN ({a}, {b}) ORDER BY `UserId` FOR UPDATE").AsNoTracking().ToListAsync(ct);

    public async Task<bool> RequestAsync(string actor, string email, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        if (email.Length > 255 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new InvalidOperationException("Enter a valid email address.");
        var recipientId = await db.Users.AsNoTracking().Where(u => u.Email.ToLower() == email).Select(u => u.UserId).SingleOrDefaultAsync(ct);
        if (recipientId == null) throw new InvalidOperationException("No eligible active user was found for this email.");
        if (recipientId == actor) throw new InvalidOperationException("You cannot send a monitoring request to yourself.");
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var accounts = await LockUsers(actor, recipientId, ct);
        var sender = accounts.SingleOrDefault(u => u.UserId == actor);
        var recipient = accounts.SingleOrDefault(u => u.UserId == recipientId);
        if (sender == null || recipient == null || !Eligible(sender) || !Eligible(recipient) || !string.Equals(recipient.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No eligible active user was found for this email.");
        var link = await db.ParentStudentLinks.SingleOrDefaultAsync(l => l.parentId == actor && l.userId == recipientId, ct);
        if (link?.linkStatus == "Pending") throw new InvalidOperationException("You already have a pending request to this user. Review it to resend or cancel.");
        if (link?.linkStatus == "Active" && link.acceptedDate.HasValue) throw new InvalidOperationException("You already have an accepted monitoring link with this user.");
        if (link?.requestedDate > now.AddMinutes(-1)) throw new InvalidOperationException("Wait one minute before sending another request to this user.");
        if (link == null)
        {
            link = new() { linkId = await ids.GenerateIdAsync("PSL"), parentId = actor, userId = recipientId, linkedDate = now };
            db.ParentStudentLinks.Add(link);
        }
        link.linkStatus = "Pending";
        link.requestVersion++;
        link.requestedDate = now;
        link.acceptedDate = null;
        link.emailSentDate = null;
        link.lastEmailAttemptDate = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await DeliverAsync(link.linkId, link.requestVersion, recipient.Email, sender.FullName, ct);
    }

    public async Task<bool?> ResendAsync(string actor, string id, int version, CancellationToken ct = default)
    {
        var pair = await db.ParentStudentLinks.AsNoTracking().Where(l => l.linkId == id && l.parentId == actor)
            .Select(l => new { l.parentId, l.userId }).SingleOrDefaultAsync(ct);
        if (pair == null) return null;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var accounts = await LockUsers(pair.parentId, pair.userId, ct);
        var link = await db.ParentStudentLinks.SingleOrDefaultAsync(l => l.linkId == id && l.parentId == actor, ct);
        if (link == null) return null;
        if (link.requestVersion != version || link.linkStatus != "Pending") throw new InvalidOperationException("This request has changed. Refresh the page.");
        if (!accounts.All(Eligible) || accounts.Count != 2) throw new InvalidOperationException("Both accounts must be active users.");
        var now = DateTime.UtcNow;
        if (link.lastEmailAttemptDate > now.AddMinutes(-1)) throw new InvalidOperationException("Wait one minute between email attempts.");
        link.lastEmailAttemptDate = now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await DeliverAsync(id, version, accounts.Single(u => u.UserId == pair.userId).Email, accounts.Single(u => u.UserId == actor).FullName, ct);
    }

    private async Task<bool> DeliverAsync(string id, int version, string email, string requester, CancellationToken ct)
    {
        try { await mail.SendRequestAsync(email, requester); }
        catch (Exception)
        {
            logger.LogWarning("Monitoring request email could not be sent for link {LinkId}. Request is retained for in-app review and resend.", id);
            return false;
        }
        await db.ParentStudentLinks.Where(l => l.linkId == id && l.requestVersion == version && l.linkStatus == "Pending")
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.emailSentDate, DateTime.UtcNow), ct);
        return true;
    }

    public async Task<bool> TransitionAsync(string actor, string id, int version, string action, CancellationToken ct = default)
    {
        var pair = await db.ParentStudentLinks.AsNoTracking().Where(l => l.linkId == id && (l.parentId == actor || l.userId == actor))
            .Select(l => new { l.parentId, l.userId }).SingleOrDefaultAsync(ct);
        if (pair == null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var accounts = await LockUsers(pair.parentId, pair.userId, ct);
        var link = await db.ParentStudentLinks.SingleOrDefaultAsync(l => l.linkId == id, ct);
        if (link == null) return false;
        if (link.requestVersion != version) throw new InvalidOperationException("This request has changed. Refresh the page before responding.");
        if (action == "Accept" || action == "Reject")
        {
            if (link.userId != actor) return false;
            if (link.linkStatus != "Pending") throw new InvalidOperationException("This request is no longer pending.");
            if (action == "Accept" && (accounts.Count != 2 || !accounts.All(Eligible))) throw new InvalidOperationException("Both accounts must be active users before a request can be accepted.");
            link.linkStatus = action == "Accept" ? "Active" : "Rejected";
            link.acceptedDate = action == "Accept" ? DateTime.UtcNow : null;
            if (action == "Accept") link.linkedDate = link.acceptedDate!.Value;
        }
        else if (action == "Cancel")
        {
            if (link.parentId != actor) return false;
            if (link.linkStatus != "Pending") throw new InvalidOperationException("Only a pending request can be cancelled.");
            link.linkStatus = "Cancelled"; link.acceptedDate = null;
        }
        else if (action == "Disconnect")
        {
            if (link.linkStatus != "Active") throw new InvalidOperationException("This relationship is no longer active.");
            link.linkStatus = "Inactive"; link.acceptedDate = null;
        }
        else return false;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }

    // Shared authorization guard for the upcoming read-only monitoring endpoints.
    public Task<bool> CanMonitorAsync(string actor, string student, CancellationToken ct = default) => db.ParentStudentLinks.AsNoTracking()
        .AnyAsync(l => l.parentId == actor && l.userId == student && l.linkStatus == "Active" && l.acceptedDate != null
            && l.Parent!.AccountStatus == AccountStatus.Active && l.Student!.AccountStatus == AccountStatus.Active
            && (l.Parent.Role == "User" || l.Parent.Role == "Parent") && (l.Student.Role == "User" || l.Student.Role == "Parent"), ct);
}
