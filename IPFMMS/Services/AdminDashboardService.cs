using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;

public sealed class AdminDashboardService(ApplicationDbContext db)
{
    public async Task<AdminDashboardViewModel> BuildAsync(string actor, CancellationToken ct = default)
    {
        await AdminGuard.RequireAsync(db, actor, ct);
        return new() {
            AsOfUtc = DateTime.UtcNow, TotalAccounts = await db.Users.CountAsync(ct),
            ActiveUsers = await db.Users.CountAsync(u => u.AccountStatus == AccountStatus.Active && (u.Role == "User" || u.Role == "Parent"), ct),
            PendingAccounts = await db.Users.CountAsync(u => u.AccountStatus == AccountStatus.Pending, ct),
            InactiveAccounts = await db.Users.CountAsync(u => u.AccountStatus == AccountStatus.Inactive, ct),
            DeletedAccounts = await db.Users.CountAsync(u => u.AccountStatus == AccountStatus.Deleted, ct),
            AdminAccounts = await db.Users.CountAsync(u => u.Role == "Admin" && u.AccountStatus == AccountStatus.Active, ct),
            TotalRelationships = await db.ParentStudentLinks.CountAsync(ct),
            ActiveRelationships = await db.ParentStudentLinks.CountAsync(l => l.linkStatus == "Active" && l.acceptedDate != null
                && l.parentHiddenDate == null && l.studentHiddenDate == null && l.Parent!.AccountStatus == AccountStatus.Active && l.Student!.AccountStatus == AccountStatus.Active
                && (l.Parent.Role == "User" || l.Parent.Role == "Parent") && (l.Student.Role == "User" || l.Student.Role == "Parent"), ct),
            PendingRelationships = await db.ParentStudentLinks.CountAsync(l => l.linkStatus == "Pending", ct),
            IncomeCategories = await db.IncomeCategories.CountAsync(ct), ExpenseCategories = await db.ExpenseCategories.CountAsync(ct),
            ActiveIncomeCategories = await db.IncomeCategories.CountAsync(c => c.IsActive, ct), ActiveExpenseCategories = await db.ExpenseCategories.CountAsync(c => c.IsActive, ct),
            IncomeRecords = await db.Incomes.CountAsync(ct), ExpenseRecords = await db.Expenses.CountAsync(ct), BudgetRecords = await db.Budgets.CountAsync(ct),
            SavingsGoals = await db.SavingsGoals.CountAsync(ct), DebtRecords = await db.Debts.CountAsync(ct), SharedGroups = await db.SharedFinances.CountAsync(ct),
            RecentActivity = await AdminUserService.AuditRows(db).OrderByDescending(a => a.CreatedDate).ThenByDescending(a => a.AuditId).Take(15).ToListAsync(ct),
            RecentAccounts = await db.Users.AsNoTracking().Where(u => u.AccountStatus != AccountStatus.Deleted).OrderByDescending(u => u.CreatedDate).ThenBy(u => u.UserId).Take(5)
                .Select(u => new AdminUserRow { UserId=u.UserId,FullName=u.FullName,Email=u.Email,PhoneNumber=u.PhoneNumber,Role=u.Role,AccountStatus=u.AccountStatus,CreatedDate=u.CreatedDate }).ToListAsync(ct)
        };
    }
}
