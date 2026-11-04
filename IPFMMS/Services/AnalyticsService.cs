using IPFMMS.Data;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Services;

public sealed class AnalyticsService(ApplicationDbContext db)
{
    public async Task<AnalyticsViewModel> BuildAsync(string userId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("An owner is required.");
        if (start.Day != 1 || end.Day != 1 || start.Year < 1000 || end.Year > 9998 || start > end ||
            (end.Year-start.Year)*12+end.Month-start.Month >= 12)
            throw new ArgumentException("A valid period of up to twelve months is required.");
        var until = end.AddMonths(1);
        var income = db.Incomes.AsNoTracking().Where(i => i.UserId == userId && i.IncomeDate >= start && i.IncomeDate < until);
        var expenses = db.Expenses.AsNoTracking().Where(e => e.UserId == userId && e.ExpenseDate >= start && e.ExpenseDate < until);
        var incomes = await income.GroupBy(i => new { i.IncomeDate.Year, i.IncomeDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(i => i.Amount) }).ToListAsync(ct);
        // Category-month grouping supports both budget matching and category totals without one query per budget.
        var expenseRows = expenses.Join(db.ExpenseCategories.AsNoTracking(), e => e.ExpenseCategoryId,
            c => c.ExpenseCategoryId, (e,c) => new { e.ExpenseDate, e.ExpenseCategoryId, c.CategoryName, e.Amount });
        var spending = await expenseRows.GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month, e.ExpenseCategoryId, e.CategoryName })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.ExpenseCategoryId, g.Key.CategoryName, Amount = g.Sum(e => e.Amount), Count = g.Count() }).ToListAsync(ct);
        var savings = await db.SavingsContributions.AsNoTracking()
            .Where(c => c.SavingsGoal!.UserId == userId && c.ContributionDate >= start && c.ContributionDate < until)
            .GroupBy(c => new { c.ContributionDate.Year, c.ContributionDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(c => c.Amount) }).ToListAsync(ct);
        var budgets = await db.Budgets.AsNoTracking().Where(b => b.UserId == userId && b.BudgetMonth >= start && b.BudgetMonth < until)
            .OrderBy(b => b.BudgetMonth).ThenBy(b => b.ExpenseCategory!.DisplayOrder).ThenBy(b => b.ExpenseCategory!.CategoryName)
            .Select(b => new { b.BudgetId, b.BudgetMonth, b.BudgetType, b.BudgetAmount, b.BudgetPercentage, b.ExpenseCategoryId, b.ExpenseCategory!.CategoryName }).ToListAsync(ct);
        var incomeByMonth = incomes.ToDictionary(i => (i.Year,i.Month), i => i.Amount);
        var expensesByMonth = spending.GroupBy(e => (e.Year,e.Month)).ToDictionary(g => g.Key,g => g.Sum(e => e.Amount));
        var savingsByMonth = savings.ToDictionary(s => (s.Year,s.Month),s => s.Amount);
        var categoryMonths = spending.ToDictionary(e => (e.Year,e.Month,e.ExpenseCategoryId),e => e.Amount);
        var model = new AnalyticsViewModel { FromMonth = start, ToMonth = end };
        for (var month = start; month <= end; month = month.AddMonths(1))
            model.Months.Add(new AnalyticsMonth { Month = month,
                Income = incomeByMonth.GetValueOrDefault((month.Year,month.Month)),
                Expenses = expensesByMonth.GetValueOrDefault((month.Year,month.Month)),
                Savings = savingsByMonth.GetValueOrDefault((month.Year,month.Month)) });
        model.Categories = spending.GroupBy(e => new { e.ExpenseCategoryId, e.CategoryName })
            .Select(g => new AnalyticsCategory { Name = g.Key.CategoryName, Amount = g.Sum(e => e.Amount), Count = g.Sum(e => e.Count) })
            .OrderByDescending(c => c.Amount).ThenBy(c => c.Name).ToList();
        foreach (var category in model.Categories)
            category.Share = model.TotalExpenses > 0m ? category.Amount / model.TotalExpenses * 100m : 0m;
        model.Budgets = budgets.Select(b => new AnalyticsBudget { Month = b.BudgetMonth, Summary = new BudgetSummaryViewModel
        {
            BudgetId = b.BudgetId, CategoryName = b.CategoryName, BudgetType = b.BudgetType,
            BudgetAmount = b.BudgetAmount, BudgetPercentage = b.BudgetPercentage,
            MonthlyIncome = incomeByMonth.GetValueOrDefault((b.BudgetMonth.Year,b.BudgetMonth.Month)),
            AmountSpent = categoryMonths.GetValueOrDefault((b.BudgetMonth.Year,b.BudgetMonth.Month,b.ExpenseCategoryId))
        }}).ToList();
        return model;
    }
}
