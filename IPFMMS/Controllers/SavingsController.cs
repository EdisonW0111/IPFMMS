using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Controllers
{
    [Authorize]
    public class SavingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IdGeneratorService _idGenerator;

        public SavingsController(ApplicationDbContext context, IdGeneratorService idGenerator)
        {
            _context = context;
            _idGenerator = idGenerator;
        }

        // GET: /Savings/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;

            // Contributions are summed through the owned goal relationship.
            // This stays one database query and does not update goal records.
            var goals = await _context.SavingsGoals.AsNoTracking()
                .Where(g => g.UserId == userId)
                .OrderByDescending(g => g.CreatedDate)
                .ThenBy(g => g.SavingsGoalId)
                .Select(g => new SavingsGoalSummaryViewModel
                {
                    SavingsGoalId = g.SavingsGoalId,
                    GoalName = g.GoalName,
                    GoalType = g.GoalType,
                    TargetAmount = g.TargetAmount,
                    TargetDate = g.TargetDate,
                    SavingsMethod = g.SavingsMethod,
                    MonthlyTargetAmount = g.MonthlyTargetAmount,
                    AutomaticSavingDay = g.AutomaticSavingDay, NextAutomaticSavingDate = g.NextAutomaticSavingDate,
                    AutomaticSavingsBaseline = g.AutomaticSavingsBaseline,
                    AutomaticPaid = _context.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == g.SavingsGoalId).Sum(a => (decimal?)a.Amount) ?? 0m,
                    AutomaticCreditUsed = _context.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == g.SavingsGoalId).Sum(a => (decimal?)a.CreditApplied) ?? 0m,
                    Status = g.Status,
                    TotalSaved = g.SavingsContributions.Sum(c => (decimal?)c.Amount) ?? 0m,
                    ContributionCount = g.SavingsContributions.Count(),
                    AsOfDate = today
                }).ToListAsync();

            return View(new SavingsIndexViewModel { Goals = goals });
        }

        // GET: /Savings/Create
        [HttpGet]
        public IActionResult Create()
        {
            if (string.IsNullOrWhiteSpace(User.FindFirstValue(ClaimTypes.NameIdentifier)))
                return Challenge();

            return View(new CreateSavingsGoalViewModel());
        }

        // POST: /Savings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("GoalName,GoalType,TargetAmount,TargetDate,SavingsMethod,AutomaticSavingDay")]
            CreateSavingsGoalViewModel model)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();

            model.GoalName = model.GoalName?.Trim() ?? string.Empty;
            // Always derive this value from the target inputs. Never trust a
            // posted monthly amount, including when returning validation errors.
            ModelState.Remove(nameof(model.MonthlyTargetAmount));
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
            model.MonthlyTargetAmount = model.SavingsMethod == "AutomaticMonthlyTarget"
                ? AutomaticSavingsPlan.Monthly(model.TargetAmount, model.TargetDate, today, model.AutomaticSavingDay)
                : null;

            // Revalidate after calculating the server-owned monthly target.
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);
            foreach (var error in errors)
            {
                var members = error.MemberNames.Any() ? error.MemberNames : new[] { string.Empty };
                foreach (var member in members)
                    if (!ModelState.TryGetValue(member, out var entry) || entry.Errors.Count == 0)
                        ModelState.AddModelError(member, error.ErrorMessage!);
            }
            if (!ModelState.IsValid) return View(model);

            // The ID counter and goal record commit together. An automatic
            // monthly target does not create a contribution or change income.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var goal = new SavingsGoal
            {
                SavingsGoalId = await _idGenerator.GenerateIdAsync("SGL"),
                UserId = userId,
                GoalName = model.GoalName,
                GoalType = model.GoalType,
                TargetAmount = model.TargetAmount!.Value,
                TargetDate = model.TargetDate!.Value.Date,
                SavingsMethod = model.SavingsMethod,
                MonthlyTargetAmount = model.SavingsMethod == "AutomaticMonthlyTarget"
                    ? model.MonthlyTargetAmount : null,
                Status = "Active",
                AutomaticSavingDay = model.SavingsMethod == "AutomaticMonthlyTarget" ? model.AutomaticSavingDay : null,
                NextAutomaticSavingDate = model.SavingsMethod == "AutomaticMonthlyTarget" ? AutomaticSavingsPlan.FirstDue(today, model.AutomaticSavingDay!.Value) : null,
                AutomaticSavingsBaseline = model.SavingsMethod == "AutomaticMonthlyTarget" ? 0m : null,
                CreatedDate = DateTime.UtcNow
            };
            _context.SavingsGoals.Add(goal);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "Savings goal created successfully.";
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public async Task<IActionResult> Contribute(string id)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var model = await GetContributionGoalAsync(id, userId);
            if (model == null) return NotFound();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contribute(
            [Bind("SavingsGoalId,Amount,ContributionDate,Description")] RecordSavingsContributionViewModel model)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();

            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), errors, true);
            foreach (var error in errors)
                foreach (var member in error.MemberNames)
                    if (!ModelState.TryGetValue(member, out var entry) || entry.Errors.Count == 0)
                        ModelState.AddModelError(member, error.ErrorMessage!);

            // Load display fields from the signed-in owner's goal even when
            // input fails validation. Other users' goal IDs return 404.
            var display = await GetContributionGoalAsync(model.SavingsGoalId, userId);
            if (display == null) return NotFound();
            model.GoalName = display.GoalName;
            model.GoalStatus = display.GoalStatus;
            model.TargetAmount = display.TargetAmount;
            model.TotalSaved = display.TotalSaved;
            if (!ModelState.IsValid) return View(model);

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            // Serialize contribution writes for this owner before rereading the
            // goal and its total. Two submissions cannot overshoot the target.
            var owners = await _context.Users.FromSqlInterpolated(
                $"SELECT * FROM `Users` WHERE `UserId` = {userId} FOR UPDATE")
                .AsNoTracking().ToListAsync();
            if (owners.Count == 0) return Challenge();
            var goal = await _context.SavingsGoals.SingleOrDefaultAsync(g =>
                g.SavingsGoalId == model.SavingsGoalId && g.UserId == userId);
            if (goal == null) return NotFound();

            decimal totalSaved = await _context.SavingsContributions
                .Where(c => c.SavingsGoalId == goal.SavingsGoalId)
                .SumAsync(c => (decimal?)c.Amount) ?? 0m;
            model.GoalName = goal.GoalName;
            model.GoalStatus = goal.Status;
            model.TargetAmount = goal.TargetAmount;
            model.TotalSaved = totalSaved;

            var available = await AvailableFundsService.BalanceAsync(_context, userId, DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date);
            if (model.Amount > available)
                ModelState.AddModelError(nameof(model.Amount), $"Contribution exceeds available funds of RM {Math.Max(0m, available):N2} after expenses and savings allocations.");
            if (!model.CanContribute)
                ModelState.AddModelError(string.Empty, "This goal is completed or inactive and cannot receive contributions.");
            else if (model.Amount!.Value > model.Remaining)
                ModelState.AddModelError(nameof(model.Amount), $"The contribution cannot exceed the remaining amount of RM{model.Remaining:N2}.");
            if (!ModelState.IsValid)
            {
                await transaction.RollbackAsync();
                return View(model);
            }

            var contribution = new SavingsContribution
            {
                ContributionId = await _idGenerator.GenerateIdAsync("SCT"),
                SavingsGoalId = goal.SavingsGoalId,
                Amount = model.Amount!.Value,
                ContributionDate = model.ContributionDate!.Value.Date,
                ContributionMethod = "Manual",
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim()
            };
            _context.SavingsContributions.Add(contribution);
            bool completed = totalSaved + contribution.Amount >= goal.TargetAmount;
            if (completed) goal.Status = "Completed";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = completed
                ? "Contribution recorded. You have reached your savings goal!"
                : "Savings contribution recorded successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Contributions(string id, int page = 1)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
            var goal = await _context.SavingsGoals.AsNoTracking()
                .Where(g => g.SavingsGoalId == id && g.UserId == userId)
                .Select(g => new SavingsGoalSummaryViewModel
                {
                    SavingsGoalId = g.SavingsGoalId,
                    GoalName = g.GoalName,
                    GoalType = g.GoalType,
                    TargetAmount = g.TargetAmount,
                    TargetDate = g.TargetDate,
                    SavingsMethod = g.SavingsMethod,
                    MonthlyTargetAmount = g.MonthlyTargetAmount,
                    AutomaticSavingDay = g.AutomaticSavingDay, NextAutomaticSavingDate = g.NextAutomaticSavingDate,
                    AutomaticSavingsBaseline = g.AutomaticSavingsBaseline,
                    AutomaticPaid = _context.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == g.SavingsGoalId).Sum(a => (decimal?)a.Amount) ?? 0m,
                    AutomaticCreditUsed = _context.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == g.SavingsGoalId).Sum(a => (decimal?)a.CreditApplied) ?? 0m,
                    Status = g.Status,
                    TotalSaved = g.SavingsContributions.Sum(c => (decimal?)c.Amount) ?? 0m,
                    ContributionCount = g.SavingsContributions.Count(),
                    AsOfDate = today
                }).SingleOrDefaultAsync();
            if (goal == null) return NotFound();

            var model = new SavingsContributionHistoryViewModel { Goal = goal };
            model.AutomaticAttempts = await _context.SavingsAutomaticAttempts.AsNoTracking()
                .Where(a => a.SavingsGoalId == id).OrderByDescending(a => a.DueDate).Take(12).ToListAsync();
            model.PageNumber = Math.Clamp(page, 1, model.TotalPages);
            model.Contributions = await _context.SavingsContributions.AsNoTracking()
                .Where(c => c.SavingsGoalId == id && c.SavingsGoal!.UserId == userId)
                .OrderByDescending(c => c.ContributionDate)
                .ThenByDescending(c => c.ContributionId)
                .Skip((model.PageNumber - 1) * SavingsContributionHistoryViewModel.PageSize)
                .Take(SavingsContributionHistoryViewModel.PageSize)
                .Select(c => new SavingsContributionRowViewModel
                {
                    ContributionId = c.ContributionId,
                    ContributionDate = c.ContributionDate,
                    Amount = c.Amount,
                    ContributionMethod = c.ContributionMethod,
                    Description = c.Description
                }).ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var model = await GetEditableGoalAsync(id, userId);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            [Bind("SavingsGoalId,GoalName,GoalType,TargetAmount,TargetDate,SavingsMethod,AutomaticSavingDay")] EditSavingsGoalViewModel model)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var existing = await GetEditableGoalAsync(model.SavingsGoalId, userId);
            if (existing == null) return NotFound();

            model.GoalName = model.GoalName?.Trim() ?? string.Empty;
            model.TotalSaved = existing.TotalSaved;
            model.ExistingMonthlyTarget = existing.ExistingMonthlyTarget;
            model.OriginalTarget = existing.OriginalTarget;
            model.OriginalDate = existing.OriginalDate;
            model.OriginalDay = existing.OriginalDay;
            // Posted display/calculation values must not replace trusted totals.
            ModelState.Remove(nameof(model.TotalSaved));
            ModelState.Remove(nameof(model.RemainingAmount));
            ModelState.Remove(nameof(model.MonthlyTargetAmount));
            ValidateEditedGoal(model);
            if (!ModelState.IsValid) return View(model);

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            // Use the same owner lock as contribution recording so a new
            // contribution cannot invalidate the target while this edit saves.
            var owners = await _context.Users.FromSqlInterpolated(
                $"SELECT * FROM `Users` WHERE `UserId` = {userId} FOR UPDATE")
                .AsNoTracking().ToListAsync();
            if (owners.Count == 0) return Challenge();
            var goal = await _context.SavingsGoals.SingleOrDefaultAsync(g =>
                g.SavingsGoalId == model.SavingsGoalId && g.UserId == userId &&
                (g.Status == "Active" || g.Status == "Completed"));
            if (goal == null) return NotFound();

            model.TotalSaved = await _context.SavingsContributions
                .Where(c => c.SavingsGoalId == goal.SavingsGoalId)
                .SumAsync(c => (decimal?)c.Amount) ?? 0m;
            ValidateEditedGoal(model);
            if (!ModelState.IsValid)
            {
                await transaction.RollbackAsync();
                return View(model);
            }

            bool replanningAutomatic = model.SavingsMethod == "AutomaticMonthlyTarget" &&
                (goal.TargetAmount != model.TargetAmount || goal.TargetDate != model.TargetDate?.Date ||
                 goal.AutomaticSavingDay != model.AutomaticSavingDay || goal.NextAutomaticSavingDate == null);
            if (replanningAutomatic)
            {
                var attempts = _context.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == goal.SavingsGoalId);
                var paid = await attempts.SumAsync(a => (decimal?)a.Amount) ?? 0m;
                var credit = await attempts.SumAsync(a => (decimal?)a.CreditApplied) ?? 0m;
                // The new monthly plan already includes all savings to date.
                goal.AutomaticSavingsBaseline = model.TotalSaved - paid - credit;
            }
            goal.GoalName = model.GoalName;
            goal.GoalType = model.GoalType;
            goal.TargetAmount = model.TargetAmount!.Value;
            goal.TargetDate = model.TargetDate!.Value.Date;
            bool startingAutomatic = model.SavingsMethod == "AutomaticMonthlyTarget" && goal.NextAutomaticSavingDate == null;
            bool dayChanged = goal.AutomaticSavingDay != model.AutomaticSavingDay;
            goal.SavingsMethod = model.SavingsMethod;
            goal.AutomaticSavingDay = model.SavingsMethod == "AutomaticMonthlyTarget" ? model.AutomaticSavingDay : null;
            if (model.SavingsMethod != "AutomaticMonthlyTarget") goal.NextAutomaticSavingDate = null;
            else if (startingAutomatic || dayChanged)
            {
                var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
                var first = AutomaticSavingsPlan.FirstDue(today, model.AutomaticSavingDay!.Value);
                // A previously processed date must never run twice, even after toggling methods.
                var last = await _context.SavingsAutomaticAttempts.Where(a => a.SavingsGoalId == goal.SavingsGoalId)
                    .MaxAsync(a => (DateTime?)a.DueDate);
                while (last.HasValue && first.Year * 12 + first.Month <= last.Value.Year * 12 + last.Value.Month) first = first.AddMonths(1);
                goal.NextAutomaticSavingDate = first;
                // Older planning-only goals start from their already-saved balance.
                goal.AutomaticSavingsBaseline ??= model.TotalSaved;
            }
            // A funded goal has no remaining monthly obligation. Store null
            // for that optional plan; the form displays RM0.00.
            goal.MonthlyTargetAmount = model.MonthlyTargetAmount > 0m
                ? model.MonthlyTargetAmount : null;
            goal.Status = model.TotalSaved >= goal.TargetAmount ? "Completed" : "Active";
            // Preserve ID, owner, creation date, and every contribution record.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = "Savings goal updated successfully. Contribution history was preserved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var goal = await _context.SavingsGoals.AsNoTracking()
                .Include(g => g.SavingsContributions)
                .SingleOrDefaultAsync(g => g.SavingsGoalId == id && g.UserId == userId);
            if (goal == null) return NotFound();
            ViewData["HasAutomaticAttempts"] = await _context.SavingsAutomaticAttempts.AnyAsync(a => a.SavingsGoalId == id);
            return View(goal);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            // Contribution recording and editing take this same owner lock.
            var owners = await _context.Users.FromSqlInterpolated(
                $"SELECT * FROM `Users` WHERE `UserId` = {userId} FOR UPDATE")
                .AsNoTracking().ToListAsync();
            if (owners.Count == 0) return Challenge();
            var goal = await _context.SavingsGoals
                .Include(g => g.SavingsContributions)
                .SingleOrDefaultAsync(g => g.SavingsGoalId == id && g.UserId == userId);
            if (goal == null) return NotFound();
            if (goal.SavingsContributions.Count != 0 || await _context.SavingsAutomaticAttempts.AnyAsync(a => a.SavingsGoalId == id))
            {
                await transaction.RollbackAsync();
                ViewData["HasAutomaticAttempts"] = true;
                ModelState.AddModelError(string.Empty, "This goal has savings history and cannot be deleted. Its savings and contribution history must be preserved.");
                return View("Delete", goal);
            }
            _context.SavingsGoals.Remove(goal);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = "Unused savings goal deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> EditContribution(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var row = await OwnedContributionAsync(id, userId);
            if (row == null) return NotFound();
            if (row.ContributionMethod != "Manual") return Forbid();
            return View(await ContributionEditModelAsync(row));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditContribution(
            [Bind("ContributionId,Amount,ContributionDate,Description")] EditSavingsContributionViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            if (!await LockContributionOwnerAsync(userId)) return Challenge();
            var row = await OwnedContributionAsync(model.ContributionId, userId);
            if (row == null) return NotFound();
            if (row.ContributionMethod != "Manual") return Forbid();
            var trusted = await ContributionEditModelAsync(row);
            model.SavingsGoalId = trusted.SavingsGoalId;
            model.GoalName = trusted.GoalName;
            model.GoalStatus = trusted.GoalStatus;
            model.TargetAmount = trusted.TargetAmount;
            model.TotalSaved = trusted.TotalSaved;
            model.OtherContributions = trusted.OtherContributions;
            foreach (var field in new[] { "SavingsGoalId", "GoalName", "GoalStatus", "TargetAmount", "TotalSaved", "OtherContributions", "MaximumAmount", "Remaining", "CanContribute" })
                ModelState.Remove(field);
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), errors, true);
            foreach (var error in errors)
                foreach (var field in error.MemberNames.DefaultIfEmpty(string.Empty))
                    ModelState.AddModelError(field, error.ErrorMessage!);
            if (row.SavingsGoal!.Status is not ("Active" or "Completed"))
                ModelState.AddModelError(string.Empty, "This goal is inactive and its contributions cannot be changed.");
            var available = await AvailableFundsService.BalanceAsync(_context, userId, DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date, row.ContributionId);
            if (model.Amount > row.Amount && model.Amount > available)
                ModelState.AddModelError(nameof(model.Amount), $"Contribution exceeds available funds of RM {Math.Max(0m, available):N2} after expenses and savings allocations.");
            if (model.Amount > model.MaximumAmount)
                ModelState.AddModelError(nameof(model.Amount), $"The amount cannot exceed RM{model.MaximumAmount:N2} after other contributions.");
            if (!ModelState.IsValid)
            {
                await tx.RollbackAsync();
                return View(model);
            }
            row.Amount = model.Amount!.Value;
            row.ContributionDate = model.ContributionDate!.Value.Date;
            row.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            row.SavingsGoal.Status = model.OtherContributions + row.Amount >= row.SavingsGoal.TargetAmount ? "Completed" : "Active";
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["SuccessMessage"] = "Contribution updated successfully.";
            return RedirectToAction(nameof(Contributions), new { id = row.SavingsGoalId });
        }

        [HttpGet]
        public async Task<IActionResult> DeleteContribution(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            var row = await OwnedContributionAsync(id, userId);
            if (row == null) return NotFound();
            if (row.ContributionMethod != "Manual") return Forbid();
            return View(await ContributionEditModelAsync(row));
        }

        [HttpPost, ActionName("DeleteContribution")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteContributionConfirmed(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();
            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            if (!await LockContributionOwnerAsync(userId)) return Challenge();
            var row = await OwnedContributionAsync(id, userId);
            if (row == null) return NotFound();
            if (row.ContributionMethod != "Manual") return Forbid();
            var model = await ContributionEditModelAsync(row);
            if (row.SavingsGoal!.Status is not ("Active" or "Completed"))
            {
                ModelState.AddModelError(string.Empty, "This goal is inactive and its contributions cannot be deleted.");
                await tx.RollbackAsync();
                return View("DeleteContribution", model);
            }
            row.SavingsGoal.Status = model.OtherContributions >= row.SavingsGoal.TargetAmount ? "Completed" : "Active";
            _context.SavingsContributions.Remove(row);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["SuccessMessage"] = "Contribution deleted. Savings totals and available funds have been updated.";
            return RedirectToAction(nameof(Contributions), new { id = row.SavingsGoalId });
        }

        private Task<SavingsContribution?> OwnedContributionAsync(string id, string userId) =>
            _context.SavingsContributions.Include(c => c.SavingsGoal)
                .SingleOrDefaultAsync(c => c.ContributionId == id && c.SavingsGoal!.UserId == userId);

        private async Task<bool> LockContributionOwnerAsync(string userId) =>
            (await _context.Users.FromSqlInterpolated(
                $"SELECT * FROM `Users` WHERE `UserId` = {userId} FOR UPDATE")
                .AsNoTracking().ToListAsync()).Count != 0;

        private async Task<EditSavingsContributionViewModel> ContributionEditModelAsync(SavingsContribution row)
        {
            var others = await _context.SavingsContributions
                .Where(c => c.SavingsGoalId == row.SavingsGoalId && c.ContributionId != row.ContributionId)
                .SumAsync(c => (decimal?)c.Amount) ?? 0m;
            return new EditSavingsContributionViewModel
            {
                ContributionId = row.ContributionId, SavingsGoalId = row.SavingsGoalId,
                GoalName = row.SavingsGoal!.GoalName, GoalStatus = row.SavingsGoal.Status,
                TargetAmount = row.SavingsGoal.TargetAmount, TotalSaved = others + row.Amount,
                OtherContributions = others, Amount = row.Amount,
                ContributionDate = row.ContributionDate, Description = row.Description
            };
        }

        private Task<EditSavingsGoalViewModel?> GetEditableGoalAsync(string id, string userId)
        {
            return _context.SavingsGoals.AsNoTracking()
                .Where(g => g.SavingsGoalId == id && g.UserId == userId &&
                    (g.Status == "Active" || g.Status == "Completed"))
                .Select(g => new EditSavingsGoalViewModel
                {
                    SavingsGoalId = g.SavingsGoalId,
                    GoalName = g.GoalName,
                    GoalType = g.GoalType,
                    TargetAmount = g.TargetAmount,
                    TargetDate = g.TargetDate,
                    SavingsMethod = g.SavingsMethod,
                    AutomaticSavingDay = g.AutomaticSavingDay,
                    ExistingMonthlyTarget = g.MonthlyTargetAmount,
                    OriginalTarget = g.TargetAmount,
                    OriginalDate = g.TargetDate,
                    OriginalDay = g.AutomaticSavingDay,
                    TotalSaved = g.SavingsContributions.Sum(c => (decimal?)c.Amount) ?? 0m
                }).SingleOrDefaultAsync();
        }

        private void ValidateEditedGoal(EditSavingsGoalViewModel model)
        {
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), errors, true);
            foreach (var error in errors)
            {
                var members = error.MemberNames.Any() ? error.MemberNames : new[] { string.Empty };
                foreach (var member in members)
                    if (!ModelState.TryGetValue(member, out var entry) || entry.Errors.Count == 0)
                        ModelState.AddModelError(member, error.ErrorMessage!);
            }
        }

        private Task<RecordSavingsContributionViewModel?> GetContributionGoalAsync(string id, string userId)
        {
            return _context.SavingsGoals.AsNoTracking()
                .Where(g => g.SavingsGoalId == id && g.UserId == userId)
                .Select(g => new RecordSavingsContributionViewModel
                {
                    SavingsGoalId = g.SavingsGoalId,
                    GoalName = g.GoalName,
                    GoalStatus = g.Status,
                    TargetAmount = g.TargetAmount,
                    TotalSaved = g.SavingsContributions.Sum(c => (decimal?)c.Amount) ?? 0m
                }).SingleOrDefaultAsync();
        }


    }
}
