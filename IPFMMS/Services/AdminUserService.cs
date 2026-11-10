using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;

public sealed class AdminUserService(ApplicationDbContext db, IdGeneratorService ids)
{
    public static string Stamp(User user) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join("\u001f", user.UserId, user.FullName, user.Email, user.PhoneNumber, user.Role, user.AccountStatus,
            user.SecurityVersion.ToString(CultureInfo.InvariantCulture), user.CreatedDate.Ticks.ToString(CultureInfo.InvariantCulture)))));

    private static AdminUserRow Row(User user) => new() {
        UserId = user.UserId, FullName = user.FullName, Email = user.Email, PhoneNumber = user.PhoneNumber,
        Role = user.Role, AccountStatus = user.AccountStatus, CreatedDate = user.CreatedDate
    };
    public async Task<AdminUserIndexViewModel> ListAsync(string actor, string? search = null, string status = "All", string role = "All", int page = 1, CancellationToken ct = default)
    {
        await AdminGuard.RequireAsync(db, actor, ct);
        search = search?.Trim() ?? "";
        if (search.Length > 100) throw new InvalidOperationException("Search must not exceed 100 characters.");
        if (!new[] { "All", "Pending", "Active", "Inactive", "Deleted" }.Contains(status)
            || !new[] { "All", "User", "Parent", "Admin" }.Contains(role))
            throw new InvalidOperationException("Choose valid account filters.");
        var query = db.Users.AsNoTracking();
        if (search.Length > 0) query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search) || u.UserId.Contains(search));
        if (status != "All") query = query.Where(u => u.AccountStatus == status);
        if (role == "User") query = query.Where(u => u.Role == "User" || u.Role == "Parent");
        else if (role != "All") query = query.Where(u => u.Role == role);
        var model = new AdminUserIndexViewModel { Search = search, Status = status, Role = role, Count = await query.CountAsync(ct) };
        model.Page = Math.Clamp(page, 1, model.Pages);
        model.Users = await query.OrderByDescending(u => u.CreatedDate).ThenBy(u => u.UserId).Skip((model.Page - 1) * 20).Take(20)
            .Select(u => new AdminUserRow { UserId=u.UserId,FullName=u.FullName,Email=u.Email,PhoneNumber=u.PhoneNumber,Role=u.Role,AccountStatus=u.AccountStatus,CreatedDate=u.CreatedDate }).ToListAsync(ct);
        return model;
    }
    public async Task<AdminUserDetailsViewModel?> DetailsAsync(string actor, string id, CancellationToken ct = default)
    {
        await AdminGuard.RequireAsync(db, actor, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == id, ct);
        if (user == null) return null;
        return new AdminUserDetailsViewModel {
            User = Row(user), IsSelf = actor == user.UserId, EditStamp = Stamp(user),
            IncomeCount = await db.Incomes.CountAsync(i => i.UserId == id, ct), ExpenseCount = await db.Expenses.CountAsync(e => e.UserId == id, ct),
            BudgetCount = await db.Budgets.CountAsync(b => b.UserId == id, ct), SavingsGoalCount = await db.SavingsGoals.CountAsync(g => g.UserId == id, ct),
            DebtCount = await db.Debts.CountAsync(d => d.userId == id, ct), SharedFinanceCount = await db.SharedFinanceMembers.CountAsync(m => m.userId == id, ct),
            MonitoringAsParentCount = await db.ParentStudentLinks.CountAsync(l => l.parentId == id, ct), MonitoringAsStudentCount = await db.ParentStudentLinks.CountAsync(l => l.userId == id, ct),
            AuditEntries = await AuditRows(db).Where(a => a.EntityType == "User" && a.EntityId == id).OrderByDescending(a => a.CreatedDate).ThenByDescending(a => a.AuditId).Take(10).ToListAsync(ct)
        };
    }
    public async Task<AdminUserEditViewModel?> EditModelAsync(string actor, string id, CancellationToken ct = default)
    {
        await AdminGuard.RequireAsync(db, actor, ct);
        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == id, ct);
        if (u == null || u.AccountStatus == AccountStatus.Deleted) return null;
        return new() { UserId = u.UserId, FullName = u.FullName, Email = u.Email, PhoneNumber = u.PhoneNumber,
            Role = u.Role, AccountStatus = u.AccountStatus, EditStamp = Stamp(u), IsSelf = actor == u.UserId,
            IsPending = u.AccountStatus == AccountStatus.Pending, IsDeleted = false };
    }
    public async Task<bool> EditAsync(string actor, string id, AdminUserEditViewModel model, CancellationToken ct = default)
    {
        if (id != model.UserId) throw new InvalidOperationException("The selected account has changed.");
        var name = (model.FullName ?? "").Trim(); var phone = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        if (name.Length == 0 || name.Length > 100 || name.Any(char.IsControl) || phone?.Length > 20
            || (phone != null && !new PhoneAttribute().IsValid(phone))) throw new InvalidOperationException("Enter a valid name and phone number.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await AdminGuard.LockAsync(db, actor, ct, new[] { id });
        var u = (await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (u == null) return false;
        await db.Entry(u).ReloadAsync(ct);
        if (u.AccountStatus == AccountStatus.Deleted) throw new InvalidOperationException("Deleted accounts are retained as read-only history.");
        if (Stamp(u) != model.EditStamp) throw new InvalidOperationException("This account changed after you opened it. Reload before saving.");
        if (model.Role != "User" && model.Role != "Admin" && !(model.Role == "Parent" && u.Role == "Parent"))
            throw new InvalidOperationException("Choose User or Admin. Every ordinary user can use consented parental monitoring.");
        if (u.AccountStatus == AccountStatus.Pending)
        {
            if (model.AccountStatus != AccountStatus.Pending || model.Role != u.Role)
                throw new InvalidOperationException("Pending accounts must finish email verification before their access or role can be changed.");
        }
        else if (model.AccountStatus != AccountStatus.Active && model.AccountStatus != AccountStatus.Inactive)
            throw new InvalidOperationException("Choose Active or Inactive. Use the separate confirmation to delete an account.");
        if (actor == u.UserId && (model.Role != "Admin" || model.AccountStatus != AccountStatus.Active))
            throw new InvalidOperationException("You cannot disable, delete or demote your own administrator account.");
        var accessChanged = u.Role != model.Role || u.AccountStatus != model.AccountStatus;
        var changed = accessChanged || u.FullName != name || u.PhoneNumber != phone;
        if (!changed) { await tx.CommitAsync(ct); return true; }
        u.FullName = name; u.PhoneNumber = phone; u.Role = model.Role; u.AccountStatus = model.AccountStatus;
        if (accessChanged)
        {
            u.SecurityVersion++;
            await db.PasswordResetTokens.Where(t => t.UserId == id && !t.IsUsed).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsUsed, true), ct);
        }
        await AdminGuard.AuditAsync(db, ids, actor, "User", u.UserId, "UpdateAccount", accessChanged
            ? $"Account access updated: role {u.Role}, status {u.AccountStatus}. Existing sessions revoked."
            : "Account profile details updated.", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    public async Task<bool> DeleteAsync(string actor, string id, string stamp, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await AdminGuard.LockAsync(db, actor, ct, new[] { id });
        var u = (await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (u == null) return false;
        await db.Entry(u).ReloadAsync(ct);
        if (actor == u.UserId) throw new InvalidOperationException("You cannot delete your own administrator account.");
        if (Stamp(u) != stamp) throw new InvalidOperationException("This account changed. Reopen the confirmation before deleting.");
        if (u.AccountStatus == AccountStatus.Deleted) throw new InvalidOperationException("This account is already deleted.");
        u.AccountStatus = AccountStatus.Deleted; u.SecurityVersion++;
        await db.PasswordResetTokens.Where(t => t.UserId == id && !t.IsUsed).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsUsed, true), ct);
        await AdminGuard.AuditAsync(db, ids, actor, "User", u.UserId, "DeleteAccount", "Account marked deleted and sign-in disabled. Financial history retained.", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    public static IQueryable<AdminAuditRow> AuditRows(ApplicationDbContext context) => context.AdminAuditEntries.AsNoTracking().Select(a => new AdminAuditRow {
        AuditId = a.AuditId, AdminUserId = a.AdminUserId, AdminName = a.Admin!.FullName, EntityType = a.EntityType,
        EntityId = a.EntityId, Action = a.Action, Summary = a.Summary, CreatedDate = a.CreatedDate
    });
}
