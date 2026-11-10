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
            await new BudgetRolloverService(_context,new IdGeneratorService(_context)).EnsureMonthAsync(userId,month);

            var incomeQuery = _context.Incomes.AsNoTracking().Where(i => i.UserId == userId);
            var expenseQuery = _context.Expenses.AsNoTracking().Where(e => e.UserId == userId);
            // Allocations are separate from expenses and include contributions
            // to completed goals. A planning target alone is never an allocation.
            var contributionQuery = _context.SavingsContributions.AsNoTracking()
                .Where(c => c.SavingsGoal!.UserId == userId);
            var sharedAllocated = await SharedSavingsAllocations.NetAsync(_context,userId);
            var sharedMonthlyAllocated = await SharedSavingsAllocations.NetAsync(_context,userId,month,nextMonth);
            var model = new FinancialDashboardViewModel
            {
                AsOfDate = today,
                TotalIncome = await incomeQuery.SumAsync(i => (decimal?)i.Amount) ?? 0m,
                TotalExpenses = await expenseQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m,
                TotalSavingsAllocated = (await contributionQuery.SumAsync(c => (decimal?)c.Amount) ?? 0m) + sharedAllocated,
                MonthlyIncome = await incomeQuery
                    .Where(i => i.IncomeDate >= month && i.IncomeDate < nextMonth)
                    .SumAsync(i => (decimal?)i.Amount) ?? 0m,
                MonthlyExpenses = await expenseQuery
                    .Where(e => e.ExpenseDate >= month && e.ExpenseDate < nextMonth)
                    .SumAsync(e => (decimal?)e.Amount) ?? 0m,
                MonthlySavingsAllocated = (await contributionQuery
                    .Where(c => c.ContributionDate >= month && c.ContributionDate < nextMonth)
                    .SumAsync(c => (decimal?)c.Amount) ?? 0m) + sharedMonthlyAllocated
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

            // Show only this owner's active, unpaid occurrences, without generating or paying any bill.
            var billLimit=today.AddDays(30);
            var unpaidBills=_context.RoutineBills.AsNoTracking().Where(b=>b.userId==userId&&b.status=="Active"
                && !_context.RoutineBillPayments.Any(p=>p.billId==b.billId&&p.dueDate==b.dueDate));
            model.UpcomingBillCount=await unpaidBills.CountAsync(b=>b.dueDate>=today&&b.dueDate<=billLimit);
            model.OverdueBillCount=await unpaidBills.CountAsync(b=>b.dueDate<today);
            model.UpcomingBills=await unpaidBills.Where(b=>b.dueDate>=today&&b.dueDate<=billLimit).OrderBy(b=>b.dueDate).ThenBy(b=>b.billId).Take(4)
                .Select(b=>new DashboardBillViewModel{BillId=b.billId,Name=b.billName,Type=b.billType,Category=b.ExpenseCategory!.CategoryName,Amount=b.amount,DueDate=b.dueDate}).ToListAsync();
            model.OverdueBills=await unpaidBills.Where(b=>b.dueDate<today).OrderBy(b=>b.dueDate).ThenBy(b=>b.billId).Take(3)
                .Select(b=>new DashboardBillViewModel{BillId=b.billId,Name=b.billName,Type=b.billType,Category=b.ExpenseCategory!.CategoryName,Amount=b.amount,DueDate=b.dueDate}).ToListAsync();

            // Read each activity from its financial source once. Bill/debt/shared expense rows
            // are already in Expenses, so their history tables are used only to choose a link.
            var cutoff=today.AddDays(1);
            var activities=await (from income in incomeQuery
                join category in _context.IncomeCategories.AsNoTracking() on income.IncomeCategoryId equals category.IncomeCategoryId
                where income.IncomeDate<cutoff
                orderby income.IncomeDate descending,income.CreatedDate descending,income.IncomeId descending
                select new DashboardActivityViewModel{Id=income.IncomeId,Date=income.IncomeDate,RecordedAt=income.CreatedDate,Type="Income",Title=category.CategoryName,Description=income.Description,Amount=income.Amount,Controller="Income",Action="Edit",RouteId=income.IncomeId}).Take(10).ToListAsync();
            var recentExpenses=await (from expense in expenseQuery
                join category in _context.ExpenseCategories.AsNoTracking() on expense.ExpenseCategoryId equals category.ExpenseCategoryId
                where expense.ExpenseDate<cutoff
                orderby expense.ExpenseDate descending,expense.CreatedDate descending,expense.ExpenseId descending
                select new{expense.ExpenseId,expense.ExpenseDate,expense.CreatedDate,expense.Description,expense.Amount,category.CategoryName,
                    Bill=_context.RoutineBillPayments.Any(p=>p.expenseId==expense.ExpenseId),Debt=_context.DebtRepayments.Any(p=>p.expenseId==expense.ExpenseId),Shared=_context.SharedFinanceTransactions.Any(t=>t.expenseId==expense.ExpenseId)}).Take(10).ToListAsync();
            activities.AddRange(recentExpenses.Select(e=>new DashboardActivityViewModel{Id=e.ExpenseId,Date=e.ExpenseDate,RecordedAt=e.CreatedDate,Type="Expense",Title=e.CategoryName,Description=e.Description,Amount=e.Amount,
                Controller=e.Shared?"SharedFinance":e.Debt?"Debt":e.Bill?"RoutineBills":"Expense",Action=e.Shared?"PersonalEntry":e.Debt?"Payment":e.Bill?"History":"Edit",RouteId=e.Shared||e.Debt||e.Bill?null:e.ExpenseId,ExpenseId=e.Shared||e.Debt||e.Bill?e.ExpenseId:null}));
            activities.AddRange(await contributionQuery.Where(c=>c.ContributionDate<cutoff).OrderByDescending(c=>c.ContributionDate).ThenByDescending(c=>c.ContributionId).Take(10)
                .Select(c=>new DashboardActivityViewModel{Id=c.ContributionId,Date=c.ContributionDate,RecordedAt=c.ContributionDate,Type="Savings",Title=c.SavingsGoal!.GoalName,Description=c.Description,Amount=c.Amount,Controller="Savings",Action="Contributions",RouteId=c.SavingsGoalId}).ToListAsync());
            activities.AddRange(await SharedSavingsAllocations.Query(_context,userId).Where(t=>t.transactionDate<cutoff).OrderByDescending(t=>t.transactionDate).ThenByDescending(t=>t.createdDate).ThenByDescending(t=>t.sharedTransactionId).Take(10)
                .Select(t=>new DashboardActivityViewModel{Id=t.sharedTransactionId,Date=t.transactionDate,RecordedAt=t.createdDate,Type=t.transactionType=="Withdrawal"?"SharedRelease":"SharedSavings",Title=t.Finance!.sharedFinanceName,Description=t.description,Amount=t.amount,Controller="SharedFinance",Action="MyEntries"}).ToListAsync());
            model.RecentActivities=activities.OrderByDescending(a=>a.Date).ThenByDescending(a=>a.RecordedAt).ThenByDescending(a=>a.Id).Take(10).ToList();

            model.FinancialHealth = await new FinancialHealthService(_context)
                .BuildAsync(userId, month, today, includeHistory: false);
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin(CancellationToken ct = default)
        {
            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            try { return View(await new AdminDashboardService(_context).BuildAsync(actor, ct)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }
    }
}