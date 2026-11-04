using System;
using System.Globalization;
using System.Data;
using MySqlConnector;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Controllers
{
    [Authorize]
    public class BudgetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IdGeneratorService _idGenerator;
        private readonly BudgetRolloverService _rollover;

        public BudgetController(
            ApplicationDbContext context,
            IdGeneratorService idGenerator,
            BudgetRolloverService rollover)
        {
            _context = context;
            _idGenerator = idGenerator;
            _rollover = rollover;
        }

        // GET: /Budget?month=yyyy-MM
        [HttpGet]
        public async Task<IActionResult> Index(string? month)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
            var selectedMonth = new DateTime(today.Year, today.Month, 1);
            if (month != null)
            {
                if (DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedMonth) &&
                    parsedMonth.Year >= 1000 && parsedMonth.Year <= 9998)
                {
                    selectedMonth = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
                }
                else
                {
                    ModelState.AddModelError("month",
                        "Please select a valid month between January 1000 and December 9998. Showing the current month.");
                }
            }

            var nextMonth = selectedMonth.AddMonths(1);
            decimal monthlyIncome = await _context.Incomes.AsNoTracking()
                .Where(i => i.UserId == userId && i.IncomeDate >= selectedMonth && i.IncomeDate < nextMonth)
                .SumAsync(i => (decimal?)i.Amount) ?? 0m;

            // Group once instead of making a separate expense query for every budget.
            var spendingByCategory = await _context.Expenses.AsNoTracking()
                .Where(e => e.UserId == userId && e.ExpenseDate >= selectedMonth && e.ExpenseDate < nextMonth)
                .GroupBy(e => e.ExpenseCategoryId)
                .Select(g => new { CategoryId = g.Key, Amount = g.Sum(e => e.Amount) })
                .ToDictionaryAsync(g => g.CategoryId, g => g.Amount);

            var budgets = await _context.Budgets.AsNoTracking()
                .Where(b => b.UserId == userId && b.BudgetMonth == selectedMonth)
                .OrderBy(b => b.ExpenseCategory!.DisplayOrder)
                .ThenBy(b => b.ExpenseCategory!.CategoryName)
                .Select(b => new
                {
                    b.BudgetId,
                    b.ExpenseCategoryId,
                    CategoryName = b.ExpenseCategory!.CategoryName,
                    b.BudgetType,
                    b.BudgetAmount,
                    b.BudgetPercentage,
                    b.Notes
                })
                .ToListAsync();

            // Load category schedules once, always scoped to the signed-in owner.
            var schedules = await _context.BudgetSchedules.AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => new { s.ExpenseCategoryId, s.IsActive, s.EndMonth, s.NextBudgetMonth, CategoryActive = s.ExpenseCategory!.IsActive })
                .ToDictionaryAsync(s => s.ExpenseCategoryId);

            var currentMonth = BudgetRolloverPolicy.LatestMonth.AddMonths(-BudgetRolloverPolicy.FutureMonths);

            var model = new BudgetIndexViewModel
            {
                RolloverLimitMonth = BudgetRolloverPolicy.LatestMonth,
                ShowRolloverLimitNotice = selectedMonth > BudgetRolloverPolicy.LatestMonth &&
                    schedules.Values.Any(s => s.IsActive && s.CategoryActive && s.NextBudgetMonth <= selectedMonth &&
                        (!s.EndMonth.HasValue || s.EndMonth >= selectedMonth)),
                BudgetMonth = selectedMonth,
                MonthlyIncome = monthlyIncome,
                MonthlyExpenses = spendingByCategory.Values.Sum(),
                Budgets = budgets.Select(b => new BudgetSummaryViewModel
                {
                    BudgetId = b.BudgetId,
                    IsRepeating = schedules.GetValueOrDefault(b.ExpenseCategoryId)?.IsActive == true &&
                        (!schedules[b.ExpenseCategoryId].EndMonth.HasValue || schedules[b.ExpenseCategoryId].EndMonth >= currentMonth),
                    RepeatUntil = schedules.GetValueOrDefault(b.ExpenseCategoryId)?.EndMonth,
                    CategoryName = b.CategoryName,
                    BudgetType = b.BudgetType,
                    BudgetAmount = b.BudgetAmount,
                    BudgetPercentage = b.BudgetPercentage,
                    MonthlyIncome = monthlyIncome,
                    AmountSpent = spendingByCategory.GetValueOrDefault(b.ExpenseCategoryId),
                    Notes = b.Notes
                }).ToList()
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_BudgetDashboard", model);

            return View(model);
        }

        // AJAX POST: create due records, then return the refreshed dashboard.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rollover(string month)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedMonth) ||
                parsedMonth.Year < 1000 || parsedMonth.Year > 9998)
                return BadRequest("Please select a valid budget month.");

            var selectedMonth = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
            // Browsing beyond the generation window is informational, not a failed rollover.
            var warnings = selectedMonth > BudgetRolloverPolicy.LatestMonth
                ? new List<string>() : await _rollover.EnsureMonthAsync(userId, selectedMonth);
            var result = await Index(month);
            var model = (result as ViewResult)?.Model as BudgetIndexViewModel
                ?? (result as PartialViewResult)?.Model as BudgetIndexViewModel;
            if (model != null)
                model.RolloverWarnings = warnings;
            return result;
        }

        // GET: /Budget/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateBudgetViewModel();

            model.ExpenseCategories =
                await GetExpenseCategoriesAsync();

            return View(model);
        }

        // POST: /Budget/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateBudgetViewModel model)
        {
            // 1. Get the logged-in user ID.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // 2. Validate and convert budget month.
            DateTime? budgetMonth = null;

            if (DateTime.TryParseExact(
                model.BudgetMonth,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedMonth) &&
                parsedMonth.Year >= 1000 && parsedMonth.Year <= 9998)
            {
                budgetMonth = new DateTime(
                    parsedMonth.Year,
                    parsedMonth.Month,
                    1);
            }
            else
            {
                ModelState.AddModelError(
                    nameof(model.BudgetMonth),
                    "Please select a valid budget month between January 1000 and December 9998.");
            }

            DateTime? endMonth = null;
            if (budgetMonth.HasValue && !BudgetRepeatUntil.TryParse(model.RepeatMonthly,
                model.RepeatUntil, budgetMonth.Value, out endMonth, out var repeatError))
                ModelState.AddModelError(nameof(model.RepeatUntil), repeatError!);
            if (!model.RepeatMonthly)
            {
                model.RepeatUntil = null;
                ModelState.Remove(nameof(model.RepeatUntil));
            }

            // 3. Validate expense category.
            bool categoryExists = await _context
                .ExpenseCategories
                .AnyAsync(c =>
                    c.ExpenseCategoryId ==
                        model.ExpenseCategoryId &&
                    c.IsActive);

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(model.ExpenseCategoryId),
                    "Please select a valid expense category.");
            }

            // 4. Validate budget type and amount.
            if (model.BudgetType == "FixedAmount")
            {
                if (!model.BudgetAmount.HasValue ||
                    model.BudgetAmount.Value <= 0)
                {
                    ModelState.AddModelError(
                        nameof(model.BudgetAmount),
                        "Budget amount must be greater than RM0.");
                }

                // Fixed budgets do not use a percentage.
                model.BudgetPercentage = null;
                ModelState.Remove(nameof(model.BudgetPercentage));
            }
            else if (model.BudgetType == "Percentage")
            {
                if (!model.BudgetPercentage.HasValue ||
                    model.BudgetPercentage.Value < 1 ||
                    model.BudgetPercentage.Value > 100)
                {
                    ModelState.AddModelError(
                        nameof(model.BudgetPercentage),
                        "Percentage must be between 1% and 100%.");
                }

                // Percentage budgets do not use a fixed amount.
                model.BudgetAmount = null;
                ModelState.Remove(nameof(model.BudgetAmount));
            }
            else
            {
                ModelState.AddModelError(
                    nameof(model.BudgetType),
                    "Please select a valid budget type.");
            }

            // Reject invalid input before opening a transaction.
            if (!ModelState.IsValid)
            {
                model.ExpenseCategories = await GetExpenseCategoriesAsync();
                return View(model);
            }

            // MySQL stores these fields with two decimal places. Reject extra
            // precision so the saved values match the allocation we validate.
            if (model.BudgetAmount is decimal amount &&
                decimal.Round(amount, 2) != amount)
            {
                ModelState.AddModelError(nameof(model.BudgetAmount),
                    "Budget amount can have at most two decimal places.");
            }
            if (model.BudgetPercentage is decimal percentage &&
                decimal.Round(percentage, 2) != percentage)
            {
                ModelState.AddModelError(nameof(model.BudgetPercentage),
                    "Percentage can have at most two decimal places.");
            }
            if (!ModelState.IsValid)
            {
                model.ExpenseCategories = await GetExpenseCategoriesAsync();
                return View(model);
            }

            await using (var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                // Serialize budget creation for this owner, even when there are
                // no budgets yet. The lock lasts until commit or rollback.
                var owners = await _context.Users
                    .FromSqlInterpolated(
                        $"SELECT * FROM `Users` WHERE `UserId` = {userId} FOR UPDATE")
                    .AsNoTracking()
                    .ToListAsync();

                if (owners.Count == 0)
                    return Challenge();

                bool duplicateExists = await _context.Budgets.AnyAsync(b =>
                    b.UserId == userId &&
                    b.ExpenseCategoryId == model.ExpenseCategoryId &&
                    b.BudgetMonth == budgetMonth!.Value);

                if (duplicateExists)
                {
                    ModelState.AddModelError(nameof(model.ExpenseCategoryId),
                        "A budget already exists for this category and month.");
                }

                if (model.BudgetType == "Percentage")
                {
                    decimal existingPercentage = await _context.Budgets
                        .Where(b => b.UserId == userId &&
                            b.BudgetMonth == budgetMonth!.Value &&
                            b.BudgetType == "Percentage")
                        .SumAsync(b => b.BudgetPercentage ?? 0m);

                    if (existingPercentage + model.BudgetPercentage!.Value > 100m)
                    {
                        ModelState.AddModelError(nameof(model.BudgetPercentage),
                            "Total percentage allocation across all categories " +
                            "cannot exceed 100%. Currently allocated: " +
                            $"{existingPercentage:0.##}%.");
                    }
                }

                BudgetSchedule? schedule = null;
                if (model.RepeatMonthly)
                {
                    // The owner lock also serializes schedule creation for this user.
                    schedule = await _context.BudgetSchedules.SingleOrDefaultAsync(s =>
                        s.UserId == userId &&
                        s.ExpenseCategoryId == model.ExpenseCategoryId);

                    if (schedule?.IsActive == true && (!schedule.EndMonth.HasValue ||
                        schedule.EndMonth.Value >= budgetMonth!.Value.AddMonths(1)))
                    {
                        ModelState.AddModelError(nameof(model.RepeatMonthly),
                            "This category already has an active repeating schedule. " +
                            "Leave Repeat monthly unchecked to create a one-off budget without changing it.");
                    }

                    if (model.BudgetType == "Percentage")
                    {
                        // Reserve allocation for other schedules that have not ended
                        // before this schedule starts.
                        decimal scheduledPercentage = await _context.BudgetSchedules
                            .Where(s => s.UserId == userId && s.IsActive &&
                                s.ExpenseCategoryId != model.ExpenseCategoryId &&
                                (!s.EndMonth.HasValue || s.EndMonth >= budgetMonth!.Value.AddMonths(1)) &&
                                s.BudgetType == "Percentage")
                            .SumAsync(s => s.BudgetPercentage ?? 0m);

                        if (scheduledPercentage + model.BudgetPercentage!.Value > 100m)
                        {
                            ModelState.AddModelError(nameof(model.RepeatMonthly),
                                "Total percentage allocation across repeating schedules cannot exceed 100%. " +
                                $"Other categories already repeat {scheduledPercentage:0.##}%.");
                        }
                    }
                }

                if (!ModelState.IsValid)
                {
                    await transaction.RollbackAsync();
                    model.ExpenseCategories = await GetExpenseCategoriesAsync();
                    return View(model);
                }

                var budget = new Budget
                {
                    BudgetId = await _idGenerator.GenerateIdAsync("BDG"),
                    UserId = userId,
                    ExpenseCategoryId = model.ExpenseCategoryId,
                    BudgetMonth = budgetMonth!.Value,
                    BudgetType = model.BudgetType,
                    BudgetAmount = model.BudgetAmount,
                    BudgetPercentage = model.BudgetPercentage,
                    Notes = model.Notes?.Trim(),
                    CreatedDate = DateTime.UtcNow
                };

                if (model.RepeatMonthly)
                {
                    if (schedule == null)
                    {
                        schedule = new BudgetSchedule
                        {
                            BudgetScheduleId = await _idGenerator.GenerateIdAsync("BSC"),
                            UserId = userId,
                            ExpenseCategoryId = model.ExpenseCategoryId,
                            CreatedDate = DateTime.UtcNow
                        };
                        _context.BudgetSchedules.Add(schedule);
                    }

                    // A new activation gets its own series, even though the category schedule is reused.
                    if (_context.Entry(schedule).State != EntityState.Added)
                        schedule.SeriesVersion++;
                    budget.BudgetScheduleId = schedule.BudgetScheduleId;
                    budget.RepeatSeriesVersion = schedule.SeriesVersion;
                    // Re-enable a disabled schedule without creating a second one.
                    schedule.BudgetType = budget.BudgetType;
                    schedule.BudgetAmount = budget.BudgetAmount;
                    schedule.BudgetPercentage = budget.BudgetPercentage;
                    schedule.Notes = budget.Notes;
                    schedule.EndMonth = endMonth;
                    schedule.NextBudgetMonth = budget.BudgetMonth.AddMonths(1);
                    schedule.IsActive = true;
                }

                _context.Budgets.Add(budget);
                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (DbUpdateException ex) when (
                    ex.InnerException is MySqlException { Number: 1062 })
                {
                    await transaction.RollbackAsync();
                    _context.Entry(budget).State = EntityState.Detached;
                    if (schedule != null)
                        _context.Entry(schedule).State = EntityState.Detached;

                    // Only translate a category/month conflict into this error.
                    // Other unique-key failures still need investigation.
                    bool duplicateNowExists = await _context.Budgets.AnyAsync(b =>
                        b.UserId == userId &&
                        b.ExpenseCategoryId == model.ExpenseCategoryId &&
                        b.BudgetMonth == budgetMonth!.Value);
                    if (!duplicateNowExists)
                        throw;

                    ModelState.AddModelError(nameof(model.ExpenseCategoryId),
                        "A budget already exists for this category and month.");
                    model.ExpenseCategories = await GetExpenseCategoriesAsync();
                    return View(model);
                }
            }

            TempData["SuccessMessage"] =
                model.RepeatMonthly
                    ? "Monthly budget created successfully. Monthly repeat settings saved."
                    : "Monthly budget created successfully.";

            return RedirectToAction(nameof(Index), new
            {
                month = budgetMonth!.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var budget = await _context.Budgets.AsNoTracking()
                .Include(b => b.ExpenseCategory)
                .SingleOrDefaultAsync(b => b.BudgetId == id && b.UserId == userId);
            if (budget == null) return NotFound();

            var repeatSchedule = await _context.BudgetSchedules.AsNoTracking().SingleOrDefaultAsync(s =>
                s.UserId == userId && s.ExpenseCategoryId == budget.ExpenseCategoryId);

            return View(new EditBudgetViewModel
            {
                BudgetId = budget.BudgetId,
                CategoryName = budget.ExpenseCategory!.CategoryName,
                BudgetMonth = budget.BudgetMonth,
                BudgetType = budget.BudgetType,
                BudgetAmount = budget.BudgetAmount,
                BudgetPercentage = budget.BudgetPercentage,
                Notes = budget.Notes,
                RepeatMonthly = repeatSchedule?.IsActive == true,
                RepeatUntil = repeatSchedule?.EndMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditBudgetViewModel model)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            // Ignore the unused field's binding errors when switching types.
            if (model.BudgetType == "FixedAmount")
            {
                model.BudgetPercentage = null;
                ModelState.Remove(nameof(model.BudgetPercentage));
            }
            else if (model.BudgetType == "Percentage")
            {
                model.BudgetAmount = null;
                ModelState.Remove(nameof(model.BudgetAmount));
            }
            // MVC skips IValidatableObject when another field fails binding.
            // Always enforce the applicable numeric rules after normalization.
            foreach (var error in model.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(model)))
                foreach (var member in error.MemberNames)
                    if (!ModelState.TryGetValue(member, out var entry) || entry.Errors.Count == 0)
                        ModelState.AddModelError(member, error.ErrorMessage!);

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            if (!await LockBudgetOwnerAsync(userId)) return Challenge();
            var budget = await _context.Budgets.Include(b => b.ExpenseCategory)
                .SingleOrDefaultAsync(b => b.BudgetId == model.BudgetId && b.UserId == userId);
            if (budget == null) return NotFound();

            // Restore display fields from the owned record, never from the form.
            model.CategoryName = budget.ExpenseCategory!.CategoryName;
            model.BudgetMonth = budget.BudgetMonth;
            ModelState.Remove(nameof(model.CategoryName));
            ModelState.Remove(nameof(model.BudgetMonth));
            var schedule = await _context.BudgetSchedules.SingleOrDefaultAsync(s =>
                s.UserId == userId && s.ExpenseCategoryId == budget.ExpenseCategoryId);

            if (!BudgetRepeatUntil.TryParse(model.RepeatMonthly, model.RepeatUntil,
                budget.BudgetMonth, out var endMonth, out var repeatError))
                ModelState.AddModelError(nameof(model.RepeatUntil), repeatError!);
            if (!model.RepeatMonthly)
            {
                model.RepeatUntil = null;
                ModelState.Remove(nameof(model.RepeatUntil));
            }

            if (model.RepeatMonthly && !budget.ExpenseCategory.IsActive)
                ModelState.AddModelError(nameof(model.RepeatMonthly), "An inactive category cannot repeat. Turn repetition off to save.");
            if (model.RepeatMonthly && budget.BudgetMonth.Year >= 9999)
                ModelState.AddModelError(nameof(model.RepeatMonthly), "This budget month cannot be repeated.");

            if (model.BudgetType == "Percentage" && model.BudgetPercentage is decimal percentage)
            {
                decimal monthlyAllocation = await _context.Budgets
                    .Where(b => b.UserId == userId && b.BudgetMonth == budget.BudgetMonth &&
                        b.BudgetId != budget.BudgetId && b.BudgetType == "Percentage")
                    .SumAsync(b => b.BudgetPercentage ?? 0m);
                if (monthlyAllocation + percentage > 100m)
                    ModelState.AddModelError(nameof(model.BudgetPercentage), "Total percentage allocation for this month cannot exceed 100%.");
                if (model.RepeatMonthly)
                {
                    decimal repeatingAllocation = await _context.BudgetSchedules
                        .Where(s => s.UserId == userId && s.IsActive &&
                            s.ExpenseCategoryId != budget.ExpenseCategoryId &&
                            (!s.EndMonth.HasValue || s.EndMonth >= budget.BudgetMonth.AddMonths(1)) && s.BudgetType == "Percentage")
                        .SumAsync(s => s.BudgetPercentage ?? 0m);
                    if (repeatingAllocation + percentage > 100m)
                        ModelState.AddModelError(nameof(model.RepeatMonthly), "Total repeating percentage allocation cannot exceed 100%.");
                }
            }

            if (!ModelState.IsValid)
            {
                await transaction.RollbackAsync();
                return View(model);
            }

            // Preserve earlier eligible months with the schedule's old settings.
            // This is atomic with editing or turning repetition off.
            if (schedule?.IsActive == true && schedule.NextBudgetMonth < budget.BudgetMonth)
            {
                var historyWarnings = await _rollover.EnsureThroughMonthInTransactionAsync(
                    userId, budget.BudgetMonth.AddMonths(-1), budget.ExpenseCategoryId);
                if (historyWarnings.Count > 0)
                {
                    await transaction.RollbackAsync();
                    foreach (var warning in historyWarnings)
                        ModelState.AddModelError(string.Empty, warning);
                    ModelState.AddModelError(string.Empty, "The edit was not saved because earlier repeating budgets could not be preserved. Resolve the reported issue first.");
                    return View(model);
                }
            }

            budget.BudgetType = model.BudgetType;
            budget.BudgetAmount = model.BudgetAmount;
            budget.BudgetPercentage = model.BudgetPercentage;
            budget.Notes = model.Notes?.Trim();

            if (model.RepeatMonthly)
            {
                if (schedule == null)
                {
                    schedule = new BudgetSchedule
                    {
                        BudgetScheduleId = await _idGenerator.GenerateIdAsync("BSC"),
                        UserId = userId,
                        ExpenseCategoryId = budget.ExpenseCategoryId
                    };
                    _context.BudgetSchedules.Add(schedule);
                }
                if (_context.Entry(schedule).State != EntityState.Added &&
                    (!schedule.IsActive || (schedule.EndMonth.HasValue && schedule.EndMonth < budget.BudgetMonth)))
                    schedule.SeriesVersion++;
                budget.BudgetScheduleId = schedule.BudgetScheduleId;
                budget.RepeatSeriesVersion = schedule.SeriesVersion;
                schedule.BudgetType = budget.BudgetType;
                schedule.BudgetAmount = budget.BudgetAmount;
                schedule.BudgetPercentage = budget.BudgetPercentage;
                schedule.Notes = budget.Notes;
                schedule.EndMonth = endMonth;
                schedule.NextBudgetMonth = budget.BudgetMonth.AddMonths(1);
                schedule.IsActive = true;
            }
            else if (schedule != null)
            {
                // Keep the disabled schedule so historical budgets cannot restart it.
                schedule.IsActive = false;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = "Budget updated. Other saved monthly budgets were preserved.";
            return RedirectToAction(nameof(Index), new { month = budget.BudgetMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture) });
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var budget = await _context.Budgets.AsNoTracking().Include(b => b.ExpenseCategory)
                .SingleOrDefaultAsync(b => b.BudgetId == id && b.UserId == userId);
            if (budget == null) return NotFound();
            var model = new DeleteBudgetViewModel
            {
                BudgetId = budget.BudgetId, CategoryName = budget.ExpenseCategory!.CategoryName,
                BudgetMonth = budget.BudgetMonth, BudgetType = budget.BudgetType,
                BudgetAmount = budget.BudgetAmount, BudgetPercentage = budget.BudgetPercentage,
                CanDeleteSeries = budget.BudgetScheduleId != null && budget.RepeatSeriesVersion.HasValue
            };
            model.FutureSeriesCount = model.CanDeleteSeries ? await _context.Budgets.CountAsync(b =>
                b.UserId == userId && b.ExpenseCategoryId == budget.ExpenseCategoryId && b.BudgetMonth > budget.BudgetMonth &&
                b.BudgetScheduleId == budget.BudgetScheduleId && b.RepeatSeriesVersion == budget.RepeatSeriesVersion) : 0;
            model.HasUnlinkedCategoryBudgets = await _context.Budgets.AnyAsync(b => b.UserId == userId &&
                b.ExpenseCategoryId == budget.ExpenseCategoryId && b.BudgetMonth >= budget.BudgetMonth && b.BudgetScheduleId == null);
            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id, string scope = "month")
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            if (scope is not ("month" or "series")) return BadRequest();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            if (!await LockBudgetOwnerAsync(userId)) return Challenge();
            var budget = await _context.Budgets.SingleOrDefaultAsync(b => b.BudgetId == id && b.UserId == userId);
            if (budget == null) return NotFound();
            var schedule = await _context.BudgetSchedules.SingleOrDefaultAsync(s =>
                s.UserId == userId && s.ExpenseCategoryId == budget.ExpenseCategoryId);
            bool currentSeries = schedule != null && budget.BudgetScheduleId == schedule.BudgetScheduleId &&
                budget.RepeatSeriesVersion == schedule.SeriesVersion;
            if (scope == "series" && (budget.BudgetScheduleId == null || !budget.RepeatSeriesVersion.HasValue))
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "This budget has no repeat-series link. Delete individual months to preserve unrelated budgets.");
                return await Delete(id);
            }
            // Only stopping the currently active series requires backfilling its earlier months.
            if (scope == "series" && currentSeries && schedule!.IsActive && schedule.NextBudgetMonth < budget.BudgetMonth)
            {
                var warnings = await _rollover.EnsureThroughMonthInTransactionAsync(
                    userId, budget.BudgetMonth.AddMonths(-1), budget.ExpenseCategoryId);
                if (warnings.Count > 0)
                {
                    await transaction.RollbackAsync();
                    foreach (var warning in warnings) ModelState.AddModelError(string.Empty, warning);
                    ModelState.AddModelError(string.Empty, "Nothing was deleted because earlier repeating budgets could not be preserved.");
                    return await Delete(id);
                }
            }
            int count = 1;
            if (scope == "series")
            {
                var series = await _context.Budgets.Where(b => b.UserId == userId &&
                    b.ExpenseCategoryId == budget.ExpenseCategoryId && b.BudgetMonth >= budget.BudgetMonth &&
                    b.BudgetScheduleId == budget.BudgetScheduleId && b.RepeatSeriesVersion == budget.RepeatSeriesVersion).ToListAsync();
                count = series.Count;
                _context.Budgets.RemoveRange(series);
                if (currentSeries) schedule!.IsActive = false;
            }
            else
            {
                // Keep a permanent skip for this series/month, even if Edit rewinds its cursor later.
                var skipId = budget.BudgetScheduleId ?? (schedule?.IsActive == true ? schedule.BudgetScheduleId : null);
                var version = budget.RepeatSeriesVersion ?? schedule?.SeriesVersion;
                if (skipId != null && version.HasValue && !await _context.BudgetRepeatSkips.AnyAsync(s =>
                    s.BudgetScheduleId == skipId && s.SeriesVersion == version && s.BudgetMonth == budget.BudgetMonth))
                    _context.BudgetRepeatSkips.Add(new BudgetRepeatSkip
                    {
                        BudgetScheduleId = skipId, SeriesVersion = version.Value, BudgetMonth = budget.BudgetMonth
                    });
                _context.Budgets.Remove(budget);
            }
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = scope == "series"
                ? $"Deleted {count} budget(s) from the selected month onward in this series. Past months and unrelated budgets were preserved."
                : "Selected month's budget deleted. Repeat settings and other months were preserved.";
            return RedirectToAction(nameof(Index), new { month = budget.BudgetMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture) });
        }

        private async Task<bool> LockBudgetOwnerAsync(string userId)
        {
            var owners = await _context.Users.FromSqlInterpolated(
                $"SELECT * FROM `Users` WHERE `UserId` = {userId} FOR UPDATE")
                .AsNoTracking().ToListAsync();
            return owners.Count != 0;
        }

        // Helper: Load active expense categories.
        private async Task<
            System.Collections.Generic.List<SelectListItem>>
            GetExpenseCategoriesAsync()
        {
            return await _context.ExpenseCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.ExpenseCategoryId,
                    Text = c.CategoryName
                })
                .ToListAsync();
        }
    }
}