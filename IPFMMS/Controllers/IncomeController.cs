using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IPFMMS.Controllers
{
    [Authorize]
    public class IncomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IdGeneratorService _idGenerator;

        public IncomeController(
            ApplicationDbContext context,
            IdGeneratorService idGenerator)
        {
            _context = context;
            _idGenerator = idGenerator;
        }

        // GET: /Income/Add
        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var model = new AddIncomeViewModel();

            await LoadCategoriesAsync(model);

            return View(model);
        }

        // POST: /Income/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            AddIncomeViewModel model)
        {
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            // Validate income date.
            if (model.IncomeDate.HasValue &&
                model.IncomeDate.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(model.IncomeDate),
                    "Income date cannot be in the future.");
            }

            // Validate selected category.
            bool categoryExists =
                await _context.IncomeCategories
                    .AnyAsync(c =>
                        c.IncomeCategoryId ==
                            model.IncomeCategoryId &&
                        c.IsActive);

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(model.IncomeCategoryId),
                    "Please select a valid income category.");
            }

            if (!IncomePaymentMethods.IsValid(model.PaymentMethod))
                ModelState.AddModelError(nameof(model.PaymentMethod), "Please select a valid payment method.");

            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model);
                return View(model);
            }

            // Generate a unique income ID.
            string incomeId =
                await _idGenerator.GenerateIdAsync("INC");

            var income = new Income
            {
                IncomeId = incomeId,
                UserId = userId,
                IncomeCategoryId = model.IncomeCategoryId,
                Amount = model.Amount!.Value,
                IncomeDate = model.IncomeDate!.Value.Date,
                Description = model.Description?.Trim(),
                PaymentMethod = model.PaymentMethod,
                Reference = string.IsNullOrWhiteSpace(model.Reference) ? null : model.Reference.Trim(),
                CreatedDate = DateTime.UtcNow
            };

            _context.Incomes.Add(income);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Income added successfully.";

            return RedirectToAction(
                "Index", "Transaction");
        }

        private async Task LoadCategoriesAsync(
            AddIncomeViewModel model)
        {
            model.IncomeCategories =
                await _context.IncomeCategories
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.CategoryName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.IncomeCategoryId,
                        Text = c.CategoryName
                    })
                    .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            // Get the currently logged-in user.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // Find only an income record belonging to this user.
            var income = await _context.Incomes
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    i.IncomeId == id &&
                    i.UserId == userId);

            if (income == null)
            {
                return NotFound();
            }

            // Populate the form with existing income information.
            var model = new EditIncomeViewModel
            {
                IncomeId = income.IncomeId,
                IncomeCategoryId = income.IncomeCategoryId,
                Amount = income.Amount,
                IncomeDate = income.IncomeDate,
                Description = income.Description,
                PaymentMethod = income.PaymentMethod,
                Reference = income.Reference
            };

            // Load category options, including an inactive category
            // if it is currently assigned to the income.
            model.IncomeCategories = await _context.IncomeCategories
                .AsNoTracking()
                .Where(c => c.IsActive ||
                            c.IncomeCategoryId == income.IncomeCategoryId)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.IncomeCategoryId,
                    Text = c.CategoryName
                })
                .ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditIncomeViewModel model)
        {
            // 1. Get the logged-in user's ID.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // 2. Retrieve the user's existing income.
            var income = await _context.Incomes
                .FirstOrDefaultAsync(i =>
                    i.IncomeId == model.IncomeId &&
                    i.UserId == userId);

            if (income == null)
            {
                return NotFound();
            }

            // 3. Validate the income date.
            if (model.IncomeDate.HasValue &&
                model.IncomeDate.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(model.IncomeDate),
                    "Income date cannot be in the future.");
            }

            // 4. Validate the selected income category.
            bool categoryExists = await _context.IncomeCategories
                .AnyAsync(c =>
                    c.IncomeCategoryId == model.IncomeCategoryId &&
                    (c.IsActive ||
                     c.IncomeCategoryId == income.IncomeCategoryId));

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(model.IncomeCategoryId),
                    "Please select a valid income category.");
            }

            if (!IncomePaymentMethods.CanEdit(model.PaymentMethod, income.PaymentMethod))
                ModelState.AddModelError(nameof(model.PaymentMethod), "Please select a valid payment method.");

            // 5. Validate the model before saving.
            if (!ModelState.IsValid)
            {
                model.IncomeCategories = await _context.IncomeCategories
                    .AsNoTracking()
                    .Where(c => c.IsActive ||
                                c.IncomeCategoryId == income.IncomeCategoryId)
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.CategoryName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.IncomeCategoryId,
                        Text = c.CategoryName
                    })
                    .ToListAsync();

                return View(model);
            }

            // 6. Update the allowed income fields.
            income.IncomeCategoryId = model.IncomeCategoryId;
            income.Amount = model.Amount!.Value;
            income.IncomeDate = model.IncomeDate!.Value.Date;
            income.Description = model.Description?.Trim();
            income.PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? null : model.PaymentMethod;
            income.Reference = string.IsNullOrWhiteSpace(model.Reference) ? null : model.Reference.Trim();

            // Do not change IncomeId, UserId or CreatedDate.

            // 7. Save changes to MySQL.
            await _context.SaveChangesAsync();

            // 8. Display a success message.
            TempData["SuccessMessage"] =
                "Income transaction updated successfully.";

            // 9. Return to the Transactions overview.
            return RedirectToAction("Index", "Transaction");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var income = await _context.Incomes
                .FirstOrDefaultAsync(i =>
                    i.IncomeId == id &&
                    i.UserId == userId);

            if (income == null)
            {
                return NotFound();
            }

            _context.Incomes.Remove(income);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Income transaction deleted successfully.";

            return RedirectToAction("Index", "Transaction");
        }
    }
}