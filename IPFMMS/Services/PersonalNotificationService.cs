using System.Security.Cryptography;
using System.Text;
using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;

public sealed class PersonalNotificationService(ApplicationDbContext db, IdGeneratorService ids, FinancialHealthService health)
{
    internal static string EventKey(string owner, string type, string source) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"personal|{owner}|{type}|{source}")));
    public static readonly string[] Types = ["PersonalBudgetOverspending", "PersonalLowHealth", "DebtReminder", "AutomaticSavings", "MonitoringInvitation", "SharedInvitation", "MonitoringDisconnected"];
    public async Task RefreshAsync(string owner, DateTime today, CancellationToken ct = default)
    {
        today = today.Date;
        var month = new DateTime(today.Year, today.Month, 1);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var users = await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (!users.Any(u => u.AccountStatus == "Active" && (u.Role == "User" || u.Role == "Parent"))) return;
        await new BudgetRolloverService(db, ids).EnsureReadyThroughMonthInTransactionAsync(owner, month, ct);
        var preferences = await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == owner, ct) ?? new UserPreference();
        var keys = (await db.Notifications.Where(n => n.userId == owner).Select(n => n.eventKey).ToListAsync(ct)).ToHashSet();
        async Task Add(string type, string source, string title, string message, bool invitation = false)
        {
            var key = EventKey(owner, type, source);
            if (!keys.Add(key)) return;
            db.Notifications.Add(new Notification { notificationId = await ids.GenerateIdAsync("NTF"), userId = owner,
                notificationType = type, title = title, message = message, eventKey = key, sourceMonth = month,
                createdDateTime = DateTime.UtcNow, showInApp = invitation || preferences.FinancialInApp,
                emailStatus = !invitation && preferences.FinancialEmail ? "Pending" : "Disabled" });
        }
        var cutoff = today.AddDays(1);
        var income = await db.Incomes.Where(i => i.UserId == owner && i.IncomeDate >= month && i.IncomeDate < cutoff).SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        var spending = await db.Expenses.Where(e => e.UserId == owner && e.ExpenseDate >= month && e.ExpenseDate < cutoff)
            .GroupBy(e => e.ExpenseCategoryId).Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) }).ToListAsync(ct);
        var budgets = await db.Budgets.AsNoTracking().Include(b => b.ExpenseCategory).Where(b => b.UserId == owner && b.BudgetMonth == month).ToListAsync(ct);
        foreach (var budget in budgets)
        {
            var limit = budget.BudgetType == "Percentage" ? decimal.Round(income * (budget.BudgetPercentage ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero) : budget.BudgetAmount ?? 0m;
            var spent = spending.SingleOrDefault(e => e.Category == budget.ExpenseCategoryId)?.Amount ?? 0m;
            if (spent > limit) await Add("PersonalBudgetOverspending", $"{month:yyyy-MM}|{budget.ExpenseCategoryId}", "Your budget limit was exceeded",
                $"Your {budget.ExpenseCategory!.CategoryName} spending for {month:MMMM yyyy} was RM {spent:N2} against a RM {limit:N2} limit, checked through {today:dd MMM yyyy}. Open Budgets for current totals.");
        }
        var score = await health.BuildAsync(owner, month, today, ct: ct, includeHistory: false);
        if (score.Overall is < 40m) await Add("PersonalLowHealth", $"{month:yyyy-MM}", "Your financial health needs attention",
            $"Your provisional {month:MMMM yyyy} score was {score.Overall:0.##}/100 on {today:dd MMM yyyy}. Open Financial Health for the latest breakdown.");
        var debts = await db.Debts.AsNoTracking().Where(d => d.userId == owner && d.status == "Active" && d.outstandingAmount > 0m && d.dueDate <= today.AddDays(7)).ToListAsync(ct);
        foreach (var debt in debts)
        {
            var stage = debt.dueDate < today ? "overdue" : debt.dueDate == today ? "due today" : "upcoming";
            await Add("DebtReminder", $"{debt.debtId}|{debt.dueDate:yyyy-MM-dd}|{stage}", $"Debt repayment {stage}",
                $"{debt.debtName} has a scheduled due date of {debt.dueDate:dd MMM yyyy}. Checked on {today:dd MMM yyyy}; open Debt Management to review the amount and record repayments.");
        }
        var attempts = await db.SavingsAutomaticAttempts.AsNoTracking().Include(a => a.SavingsGoal).Where(a => a.SavingsGoal!.UserId == owner && a.DueDate >= month && a.DueDate < month.AddMonths(1)).ToListAsync(ct);
        foreach (var attempt in attempts)
            await Add("AutomaticSavings", $"{attempt.SavingsGoalId}|{attempt.DueDate:yyyy-MM-dd}|{attempt.Status}",
                attempt.Status == "InsufficientFunds" ? "Automatic saving needs funds" : "Automatic savings result",
                $"{attempt.SavingsGoal!.GoalName}, scheduled {attempt.DueDate:dd MMM yyyy}: " + (attempt.Status switch {
                    "Saved" => $"RM {attempt.Amount:N2} was allocated to savings.", "CoveredByExtra" => "Existing extra contributions covered this allocation.",
                    "InsufficientFunds" => "Available funds were insufficient. The system will retry while running during this month, up to the target date.",
                    _ => "The scheduled date was missed. Open Savings Goals to review your plan." }));
        var links = await db.ParentStudentLinks.AsNoTracking().Where(l => l.userId == owner && l.linkStatus == "Pending" && l.studentHiddenDate == null && l.parentHiddenDate == null && l.Parent!.AccountStatus == "Active").ToListAsync(ct);
        foreach (var link in links) await Add("MonitoringInvitation", $"{link.linkId}|{link.requestVersion}", "Monitoring request received", "You received a request to monitor your finances. Open Parental Monitoring to review its current status and accept or reject it yourself.", true);
        var availableGroups = db.SharedFinanceMembers.Where(m => m.memberRole == "Owner" && m.invitationStatus == "Accepted" && m.User!.AccountStatus == "Active" && (m.User.Role == "User" || m.User.Role == "Parent")).Select(m => m.sharedFinanceId);
        var invitations = await db.SharedFinanceMembers.AsNoTracking().Where(m => m.userId == owner && m.invitationStatus == "Pending" && m.Finance!.status == "Active" && availableGroups.Contains(m.sharedFinanceId)).ToListAsync(ct);
        foreach (var invite in invitations) await Add("SharedInvitation", $"{invite.sharedFinanceId}|{invite.invitationVersion}", "Shared finance invitation received", "You received a shared finance invitation. Open Shared Finance to review its current status and decide whether to join.", true);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
