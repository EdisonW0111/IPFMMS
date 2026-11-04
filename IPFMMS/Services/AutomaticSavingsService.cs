using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Services;

public sealed class AutomaticSavingsService(ApplicationDbContext db, IdGeneratorService ids)
{
    public async Task ProcessDueAsync(DateTime today, CancellationToken cancellationToken = default)
    {
        today = today.Date;
        var due = await db.SavingsGoals.AsNoTracking()
            .Where(g => g.User!.AccountStatus == "Active" && g.Status == "Active" && g.SavingsMethod == "AutomaticMonthlyTarget" &&
                g.NextAutomaticSavingDate != null && g.NextAutomaticSavingDate <= today)
            .OrderBy(g => g.NextAutomaticSavingDate).ThenBy(g => g.SavingsGoalId)
            .Select(g => g.SavingsGoalId).Take(100).ToListAsync(cancellationToken);
        foreach (var id in due)
        {
            // Every goal gets its own atomic transaction, bounded to 12 dates per run.
            for (int count = 0; count < 12; count++)
            {
                db.ChangeTracker.Clear();
                if (!await ProcessOneAsync(id, today, cancellationToken)) break;
            }
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
            goal.NextAutomaticSavingDate is not DateTime date || date > today ||
            goal.AutomaticSavingDay is not (>= 1 and <= 28) || goal.MonthlyTargetAmount is not > 0m)
            return false;
        if (date > goal.TargetDate.Date)
        {
            goal.NextAutomaticSavingDate = null;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return false;
        }
        bool exists = await db.SavingsAutomaticAttempts.AnyAsync(a => a.SavingsGoalId == id && a.DueDate.Year == date.Year && a.DueDate.Month == date.Month, ct);
        if (exists)
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
        var attempt = new SavingsAutomaticAttempt
        {
            SavingsGoalId = id, DueDate = date, PlannedAmount = goal.MonthlyTargetAmount.Value,
            ProcessedAtUtc = DateTime.UtcNow
        };
        if (date < today)
            attempt.Status = "MissedOffline";
        else
        {
            decimal income = await db.Incomes.Where(i => i.UserId == owner && i.IncomeDate <= today)
                .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
            decimal expenses = await db.Expenses.Where(e => e.UserId == owner && e.ExpenseDate <= today)
                .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
            decimal allocated = await db.SavingsContributions.Where(c => c.SavingsGoal!.UserId == owner)
                .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
            if (plan.Deduction > 0m && income-expenses-allocated < plan.Deduction)
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
        db.SavingsAutomaticAttempts.Add(attempt);
        goal.NextAutomaticSavingDate = date.AddMonths(1);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
