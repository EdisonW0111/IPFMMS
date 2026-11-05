using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPFMMS.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction(nameof(Admin));
            }

            if (User.IsInRole("User"))
            {
                return RedirectToAction(nameof(Finance));
            }

            return Forbid();
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Finance()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
            var month = new DateTime(today.Year, today.Month, 1);
            var nextMonth = month.AddMonths(1);

            var incomeQuery = _context.Incomes.AsNoTracking().Where(i => i.UserId == userId);
            var expenseQuery = _context.Expenses.AsNoTracking().Where(e => e.UserId == userId);
            // Allocations are separate from expenses and include contributions
            // to completed goals. A planning target alone is never an allocation.
            var contributionQuery = _context.SavingsContributions.AsNoTracking()
                .Where(c => c.SavingsGoal!.UserId == userId);
            var model = new FinancialDashboardViewModel
            {
                AsOfDate = today,
                TotalIncome = await incomeQuery.SumAsync(i => (decimal?)i.Amount) ?? 0m,
                TotalExpenses = await expenseQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m,
                TotalSavingsAllocated = await contributionQuery.SumAsync(c => (decimal?)c.Amount) ?? 0m,
                MonthlyIncome = await incomeQuery
                    .Where(i => i.IncomeDate >= month && i.IncomeDate < nextMonth)
                    .SumAsync(i => (decimal?)i.Amount) ?? 0m,
                MonthlyExpenses = await expenseQuery
                    .Where(e => e.ExpenseDate >= month && e.ExpenseDate < nextMonth)
                    .SumAsync(e => (decimal?)e.Amount) ?? 0m,
                MonthlySavingsAllocated = await contributionQuery
                    .Where(c => c.ContributionDate >= month && c.ContributionDate < nextMonth)
                    .SumAsync(c => (decimal?)c.Amount) ?? 0m
            };

            var spending = await expenseQuery
                .Where(e => e.ExpenseDate >= month && e.ExpenseDate < nextMonth)
                .GroupBy(e => e.ExpenseCategoryId)
                .Select(g => new { CategoryId = g.Key, Amount = g.Sum(e => e.Amount) })
                .ToDictionaryAsync(g => g.CategoryId, g => g.Amount);
            var budgets = await _context.Budgets.AsNoTracking()
                .Where(b => b.UserId == userId && b.BudgetMonth == month)
                .OrderBy(b => b.ExpenseCategory!.DisplayOrder)
                .ThenBy(b => b.ExpenseCategory!.CategoryName)
                .Select(b => new
                {
                    b.BudgetId, b.ExpenseCategoryId,
                    CategoryName = b.ExpenseCategory!.CategoryName,
                    b.BudgetType, b.BudgetAmount, b.BudgetPercentage, b.Notes
                }).ToListAsync();
            model.BudgetSummary = new BudgetIndexViewModel
            {
                BudgetMonth = month,
                RolloverLimitMonth = BudgetRolloverPolicy.LatestMonth,
                MonthlyIncome = model.MonthlyIncome,
                MonthlyExpenses = model.MonthlyExpenses,
                Budgets = budgets.Select(b => new BudgetSummaryViewModel
                {
                    BudgetId = b.BudgetId, CategoryName = b.CategoryName,
                    BudgetType = b.BudgetType, BudgetAmount = b.BudgetAmount,
                    BudgetPercentage = b.BudgetPercentage, Notes = b.Notes,
                    MonthlyIncome = model.MonthlyIncome,
                    AmountSpent = spending.GetValueOrDefault(b.ExpenseCategoryId)
                }).ToList()
            };

            model.SavingsSummary.Goals = await _context.SavingsGoals.AsNoTracking()
                .Where(g => g.UserId == userId)
                .OrderBy(g => g.TargetDate).ThenBy(g => g.SavingsGoalId)
                .Select(g => new SavingsGoalSummaryViewModel
                {
                    SavingsGoalId = g.SavingsGoalId, GoalName = g.GoalName,
                    GoalType = g.GoalType, TargetAmount = g.TargetAmount,
                    TargetDate = g.TargetDate, SavingsMethod = g.SavingsMethod,
                    MonthlyTargetAmount = g.MonthlyTargetAmount,
                    AutomaticSavingDay = g.AutomaticSavingDay, NextAutomaticSavingDate = g.NextAutomaticSavingDate,
                    AutomaticSavingsBaseline = g.AutomaticSavingsBaseline,
                    AutomaticPaid = _context.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == g.SavingsGoalId).Sum(a => (decimal?)a.Amount) ?? 0m,
                    AutomaticCreditUsed = _context.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == g.SavingsGoalId).Sum(a => (decimal?)a.CreditApplied) ?? 0m, Status = g.Status,
                    TotalSaved = g.SavingsContributions.Sum(c => (decimal?)c.Amount) ?? 0m,
                    ContributionCount = g.SavingsContributions.Count(), AsOfDate = today
                }).ToListAsync();

            model.FinancialHealth = await new FinancialHealthService(_context)
                .BuildAsync(userId, month, today, includeHistory: false);
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Admin()
        {
            return View();
        }
    }
}