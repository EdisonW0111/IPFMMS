using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Services;

public sealed class AutomaticSavingsService(ApplicationDbContext db, IdGeneratorService ids)
{
    public async Task ProcessDueAsync(DateTime today, CancellationToken cancellationToken = default)
    {
        today = today.Date;
        string? cursor = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var due = await db.SavingsGoals.AsNoTracking()
                .Where(g => (cursor == null || g.SavingsGoalId.CompareTo(cursor) > 0) && g.User!.AccountStatus == "Active" && g.Status == "Active" && g.SavingsMethod == "AutomaticMonthlyTarget" &&
                    ((g.NextAutomaticSavingDate != null && g.NextAutomaticSavingDate <= today) ||
                     db.SavingsAutomaticAttempts.Any(a => a.SavingsGoalId == g.SavingsGoalId &&
                        (a.Status == "InsufficientFunds" || a.Status == "MissedOffline") && a.DueDate.Year == today.Year &&
                        a.DueDate.Month == today.Month && a.DueDate <= today && g.TargetDate >= today)))
                .OrderBy(g => g.SavingsGoalId)
                .Select(g => g.SavingsGoalId).Take(100).ToListAsync(cancellationToken);
            if (due.Count == 0) break;
            foreach (var id in due)
            {
                // Every goal gets its own atomic transaction, bounded to 12 dates per run.
                for (int count = 0; count < 12; count++)
                {
                    db.ChangeTracker.Clear();
                    if (!await ProcessOneAsync(id, today, cancellationToken)) break;
                }
            }
            cursor = due[^1];
        }
    }

    private async Task<bool> ProcessOneAsync(string id, DateTime today, CancellationToken ct)
    {
        var owner = await db.SavingsGoals.AsNoTracking().Where(g => g.SavingsGoalId == id)
            .Select(g => g.UserId).SingleOrDefaultAsync(ct);
        if (owner == null) return false;
        // Serializable protects the balance reads against simultaneous income/expense changes.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var users = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM `Users` WHERE `UserId` = {owner} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        if (users.Count == 0 || users[0].AccountStatus != "Active") return false;
        var goal = await db.SavingsGoals.SingleOrDefaultAsync(g => g.SavingsGoalId == id && g.UserId == owner, ct);
        if (goal == null || goal.Status != "Active" || goal.SavingsMethod != "AutomaticMonthlyTarget" ||
            goal.NextAutomaticSavingDate is not DateTime date ||
            goal.AutomaticSavingDay is not (>= 1 and <= 28) || goal.MonthlyTargetAmount is not > 0m)
            return false;
        var retry = await db.SavingsAutomaticAttempts.AsNoTracking().Where(a => a.SavingsGoalId == id &&
            (a.Status == "InsufficientFunds" || a.Status == "MissedOffline") && a.DueDate.Year == today.Year &&
            a.DueDate.Month == today.Month && a.DueDate <= today).OrderBy(a => a.DueDate).FirstOrDefaultAsync(ct);
        if (retry != null && retry.DueDate < date && today <= goal.TargetDate.Date) date = retry.DueDate;
        if (date > today) return false;
        if (date > goal.TargetDate.Date)
        {
            goal.NextAutomaticSavingDate = null;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return false;
        }
        var previous = await db.SavingsAutomaticAttempts.SingleOrDefaultAsync(a => a.SavingsGoalId == id && a.DueDate.Year == date.Year && a.DueDate.Month == date.Month, ct);
        if (previous != null && ((previous.Status != "InsufficientFunds" && previous.Status != "MissedOffline") || date.Year != today.Year || date.Month != today.Month))
        {
            goal.NextAutomaticSavingDate = date.AddMonths(1);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        decimal saved = await db.SavingsContributions.Where(c => c.SavingsGoalId == id)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        if (saved >= goal.TargetAmount)
        {
            goal.Status = "Completed";
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return false;
        }
        var attempts = db.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == id);
        decimal paid = await attempts.SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
        decimal used = await attempts.SumAsync(a => (decimal?)a.CreditApplied, ct) ?? 0m;
        var plan = AutomaticSavingsPlan.Calculate(goal.MonthlyTargetAmount.Value, goal.TargetAmount-saved,
            saved, goal.AutomaticSavingsBaseline ?? saved, paid, used);
        var attempt = previous ?? new SavingsAutomaticAttempt
        {
            SavingsGoalId = id, DueDate = date, PlannedAmount = goal.MonthlyTargetAmount.Value,
            ProcessedAtUtc = DateTime.UtcNow
        };
        attempt.ProcessedAtUtc = DateTime.UtcNow;
        if (date.Year != today.Year || date.Month != today.Month || today > goal.TargetDate.Date)
            attempt.Status = "MissedOffline";
        else
        {
            var available = await AvailableFundsService.BalanceAsync(db, owner, today, ct: ct);
            if (plan.Deduction > 0m && available < plan.Deduction)
                attempt.Status = "InsufficientFunds";
            else
            {
                attempt.CreditApplied = plan.Credit;
                attempt.Amount = plan.Deduction;
                attempt.Status = plan.Deduction == 0m ? "CoveredByExtra" : "Saved";
                if (plan.Deduction > 0m)
                    db.SavingsContributions.Add(new SavingsContribution
                    {
                        ContributionId = await ids.GenerateIdAsync("SCT"), SavingsGoalId = id,
                        Amount = plan.Deduction, ContributionDate = today,
                        ContributionMethod = "Automatic", Description = $"Automatic monthly saving for {date:yyyy-MM-dd}"
                    });
                if (saved+plan.Deduction >= goal.TargetAmount) goal.Status = "Completed";
            }
        }
        if (previous == null) db.SavingsAutomaticAttempts.Add(attempt);
        goal.NextAutomaticSavingDate = attempt.Status == "InsufficientFunds" ? date : date.AddMonths(1);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return attempt.Status != "InsufficientFunds";
    }
}
