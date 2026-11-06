using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class MonitoringAlertService(ApplicationDbContext db, IdGeneratorService ids,
    MonitoringLinkService links, FinancialHealthService health, NotificationEmailService email)
{
    public const decimal LowHealthThreshold = 40m;
    public async Task RefreshAsync(string recipient, DateTime today, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipient)) throw new ArgumentException("A recipient is required.");
        var month = new DateTime(today.Year, today.Month, 1);
        var cutoff = today.Date.AddDays(1);
        var candidates = await db.ParentStudentLinks.AsNoTracking()
            .Where(l => l.parentId == recipient && l.linkStatus == "Active" && l.acceptedDate != null)
            .OrderBy(l => l.linkId).Select(l => new { l.linkId, l.userId, l.requestVersion }).ToListAsync(ct);
        foreach (var candidate in candidates)
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` IN ({recipient}, {candidate.userId}) ORDER BY `UserId` FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            var link = await db.ParentStudentLinks.AsNoTracking().SingleOrDefaultAsync(l => l.linkId == candidate.linkId, ct);
            if (link == null || link.requestVersion != candidate.requestVersion || !await links.CanMonitorAsync(recipient, candidate.userId, ct)) continue;
            var name = await db.Users.Where(u => u.UserId == candidate.userId).Select(u => u.FullName).SingleAsync(ct);
            var income = await db.Incomes.Where(i => i.UserId == candidate.userId && i.IncomeDate >= month && i.IncomeDate < cutoff)
                .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
            var expenses = await db.Expenses.Where(e => e.UserId == candidate.userId && e.ExpenseDate >= month && e.ExpenseDate < cutoff)
                .GroupBy(e => e.ExpenseCategoryId).Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) }).ToListAsync(ct);
            var budgets = await db.Budgets.AsNoTracking().Where(b => b.UserId == candidate.userId && b.BudgetMonth == month)
                .Select(b => new { b.ExpenseCategoryId, Category = b.ExpenseCategory!.CategoryName, b.BudgetType, b.BudgetAmount, b.BudgetPercentage }).ToListAsync(ct);
            var existing = await db.Notifications.Where(n => n.userId == recipient && n.linkId == link.linkId && n.requestVersion == link.requestVersion && n.sourceMonth == month)
                .Select(n => n.eventKey).ToListAsync(ct);
            var keys = existing.ToHashSet(StringComparer.Ordinal);
            async Task Add(string type, string subject, string title, string message)
            {
                var raw = $"{recipient}|{link.linkId}|{link.requestVersion}|{month:yyyy-MM}|{type}|{subject}";
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
                if (!keys.Add(key)) return;
                db.Notifications.Add(new()
                {
                    notificationId = await ids.GenerateIdAsync("NTF"), userId = recipient, relatedUserId = candidate.userId,
                    notificationType = type, title = title, message = message, createdDateTime = DateTime.UtcNow,
                    eventKey = key, linkId = link.linkId, requestVersion = link.requestVersion, sourceMonth = month, emailStatus = "Pending"
                });
            }
            string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
            foreach (var budget in budgets)
            {
                var limit = budget.BudgetType == "Percentage" ? decimal.Round(income * (budget.BudgetPercentage ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero) : budget.BudgetAmount ?? 0m;
                var spent = expenses.SingleOrDefault(e => e.Category == budget.ExpenseCategoryId)?.Amount ?? 0m;
                if (spent > limit)
                    await Add("BudgetOverspending", budget.ExpenseCategoryId, "Budget limit exceeded",
                        $"{name}'s {budget.Category} spending for {month:MMMM yyyy} was RM {Money(spent)} against a RM {Money(limit)} budget (RM {Money(spent - limit)} over), checked through {today:dd MMM yyyy}. This records the first detected overspending for this category and month; open the summary for current figures.");
            }
            var score = await health.BuildAsync(candidate.userId, month, today, ct: ct, includeHistory: false);
            if (score.Overall.HasValue && score.Overall.Value < LowHealthThreshold)
                await Add("LowFinancialHealth", "score", "Low Financial Health · provisional",
                    $"{name}'s provisional Financial Health score for {month:MMMM yyyy} was {score.Overall.Value.ToString("0.##", CultureInfo.InvariantCulture)} / 100, below the {LowHealthThreshold:0}-point Needs attention threshold, checked through {today:dd MMM yyyy}. This current-month score can change as records are added. Open the summary for the latest breakdown.");
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        await email.DeliverPendingAsync(recipient, ct);
    }
}
