using IPFMMS.Data;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class MonitoringSummaryService(ApplicationDbContext db, MonitoringLinkService links,
    FinancialHealthService health, SpendingPredictionService predictions)
{
    public async Task<MonitoringSummaryViewModel?> BuildAsync(string viewer, string student, DateTime month, DateTime today, CancellationToken ct = default)
    {
        var current = new DateTime(today.Year, today.Month, 1);
        if (month < new DateTime(1000, 4, 1) || month > current || month.Day != 1 || month.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException("Choose a valid past or current month.");
        // Shared account locks serialize this read with disconnect/disable operations.
        // No rows are inserted, updated or deleted by this transaction.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM `Users` WHERE `UserId` IN ({viewer}, {student}) ORDER BY `UserId` LOCK IN SHARE MODE")
            .AsNoTracking().ToListAsync(ct);
        if (!await links.CanMonitorAsync(viewer, student, ct)) return null;
        var cutoff = month == current ? today.Date.AddDays(1) : month.AddMonths(1);
        var tomorrow = today.Date.AddDays(1);
        var model = new MonitoringSummaryViewModel
        {
            StudentId = student, StudentName = await db.Users.Where(u => u.UserId == student).Select(u => u.FullName).SingleAsync(ct),
            Month = month, AsOfDate = today.Date,
            Income = await db.Incomes.Where(i => i.UserId == student && i.IncomeDate >= month && i.IncomeDate < cutoff).SumAsync(i => (decimal?)i.Amount, ct) ?? 0m,
            Expenses = await db.Expenses.Where(e => e.UserId == student && e.ExpenseDate >= month && e.ExpenseDate < cutoff).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m,
            SavingsContributions = await db.SavingsContributions.Where(c => c.SavingsGoal!.UserId == student && c.ContributionDate >= month && c.ContributionDate < cutoff).SumAsync(c => (decimal?)c.Amount, ct) ?? 0m
        };
        var categories = await db.Expenses.AsNoTracking().Where(e => e.UserId == student && e.ExpenseDate >= month && e.ExpenseDate < cutoff)
            .GroupBy(e => e.ExpenseCategoryId).Select(g => new { Id = g.Key, Amount = g.Sum(e => e.Amount) }).ToListAsync(ct);
        var names = await db.ExpenseCategories.AsNoTracking().Select(c => new { c.ExpenseCategoryId, c.CategoryName }).ToDictionaryAsync(c => c.ExpenseCategoryId, c => c.CategoryName, ct);
        model.Categories = categories.Select(c => new MonitoringCategorySpending { Name = names.GetValueOrDefault(c.Id, "Other"), Amount = c.Amount })
            .OrderByDescending(c => c.Amount).ThenBy(c => c.Name).ToList();
        var savedBudgets = await db.Budgets.AsNoTracking().Where(b => b.UserId == student && b.BudgetMonth == month)
            .Select(b => new { b.BudgetId, b.ExpenseCategoryId, b.BudgetType, b.BudgetAmount, b.BudgetPercentage }).ToListAsync(ct);
        model.Budgets = new BudgetIndexViewModel
        {
            BudgetMonth = month, MonthlyIncome = model.Income, MonthlyExpenses = model.Expenses,
            Budgets = savedBudgets.Select(b => new BudgetSummaryViewModel
            {
                CategoryName = names.GetValueOrDefault(b.ExpenseCategoryId, "Other"), BudgetType = b.BudgetType,
                BudgetAmount = b.BudgetAmount, BudgetPercentage = b.BudgetPercentage, MonthlyIncome = model.Income,
                AmountSpent = categories.SingleOrDefault(c => c.Id == b.ExpenseCategoryId)?.Amount ?? 0m
            }).OrderBy(b => b.CategoryName).ToList()
        };
        model.GoalCount = await db.SavingsGoals.CountAsync(g => g.UserId == student, ct);
        model.Goals = await db.SavingsGoals.AsNoTracking().Where(g => g.UserId == student).OrderBy(g => g.TargetDate).ThenBy(g => g.SavingsGoalId).Take(20)
            .Select(g => new SavingsGoalSummaryViewModel
            {
                GoalName = g.GoalName, TargetAmount = g.TargetAmount, TargetDate = g.TargetDate, Status = g.Status, AsOfDate = today,
                TotalSaved = g.SavingsContributions.Where(c => c.ContributionDate < tomorrow).Sum(c => (decimal?)c.Amount) ?? 0m
            }).ToListAsync(ct);
        model.Health = await health.BuildAsync(student, month, today, ct: ct, includeHistory: false);
        model.Prediction = await predictions.BuildAsync(student, month, today, ct);
        await tx.CommitAsync(ct);
        return model;
    }
}
