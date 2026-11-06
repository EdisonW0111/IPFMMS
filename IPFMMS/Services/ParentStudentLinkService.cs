using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class ParentStudentLinkService(ApplicationDbContext db, IdGeneratorService ids)
{
    public async Task CreateAsync(string parentId, string studentId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await ValidateAccountsAsync(parentId, studentId, ct);
        var existing = await db.ParentStudentLinks.SingleOrDefaultAsync(l => l.parentId == parentId && l.userId == studentId, ct);
        if (existing != null)
            throw new InvalidOperationException(existing.linkStatus == "Active" ? "These accounts are already linked." : "This relationship already exists. Reactivate it from the relationship list.");
        db.ParentStudentLinks.Add(new() { linkId = await ids.GenerateIdAsync("PSL"), parentId = parentId, userId = studentId, linkedDate = DateTime.UtcNow, linkStatus = "Active" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<bool> SetActiveAsync(string id, bool active, CancellationToken ct = default)
    {
        // Read identities first, then lock the account rows in a consistent order.
        var pair = await db.ParentStudentLinks.AsNoTracking().Where(l => l.linkId == id).Select(l => new { l.parentId, l.userId }).SingleOrDefaultAsync(ct);
        if (pair == null) return false;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (active) await ValidateAccountsAsync(pair.parentId, pair.userId, ct);
        else await LockAccountsAsync(pair.parentId, pair.userId, ct);
        var link = await db.ParentStudentLinks.SingleOrDefaultAsync(l => l.linkId == id, ct);
        if (link == null) return false;
        link.linkStatus = active ? "Active" : "Inactive";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private Task<List<User>> LockAccountsAsync(string parentId, string studentId, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` IN ({parentId}, {studentId}) ORDER BY `UserId` FOR UPDATE").AsNoTracking().ToListAsync(ct);

    private async Task ValidateAccountsAsync(string parentId, string studentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(studentId) || parentId == studentId || parentId.Length > 20 || studentId.Length > 20)
            throw new InvalidOperationException("Select two different valid accounts.");
        var accounts = await LockAccountsAsync(parentId, studentId, ct);
        if (!accounts.Any(u => u.UserId == parentId && (u.Role == "User" || u.Role == "Parent") && u.AccountStatus == AccountStatus.Active))
            throw new InvalidOperationException("Select an active user to monitor the linked student.");
        if (!accounts.Any(u => u.UserId == studentId && (u.Role == "User" || u.Role == "Parent") && u.AccountStatus == AccountStatus.Active))
            throw new InvalidOperationException("Select an active user to be monitored.");
    }
}
