using IPFMMS.Data;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IPFMMS.Controllers
{
    [Authorize]
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransactionController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Transaction/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            // Retrieve the current user's income records.
            var incomes = await (
                from income in _context.Incomes.AsNoTracking()
                join category in _context.IncomeCategories.AsNoTracking()
                    on income.IncomeCategoryId
                    equals category.IncomeCategoryId
                where income.UserId == userId
                select new TransactionViewModel
                {
                    TransactionId = income.IncomeId,
                    TransactionType = "Income",
                    CategoryName = category.CategoryName,
                    Amount = income.Amount,
                    TransactionDate = income.IncomeDate,
                    Description = income.Description
                }
            ).ToListAsync();

            // Retrieve the current user's expense records.
            var expenses = await (
                from expense in _context.Expenses.AsNoTracking()
                join category in _context.ExpenseCategories.AsNoTracking()
                    on expense.ExpenseCategoryId
                    equals category.ExpenseCategoryId
                where expense.UserId == userId
                select new TransactionViewModel
                {
                    TransactionId = expense.ExpenseId,
                    TransactionType = "Expense",
                    CategoryName = category.CategoryName,
                    Amount = expense.Amount,
                    TransactionDate = expense.ExpenseDate,
                    Description = expense.Description,
                    HasReceipt = expense.ReceiptPath != null && expense.ReceiptPath != ""
                }
            ).ToListAsync();

            var model = new TransactionIndexViewModel
            {
                TotalIncome = incomes.Sum(i => i.Amount),
                TotalExpenses = expenses.Sum(e => e.Amount),

                Transactions = incomes
        .Concat(expenses)
        .OrderByDescending(t => t.TransactionDate)
        .ThenByDescending(t => t.TransactionId)
        .Take(10)
        .ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> All()
        {
            // Get the currently logged-in user's ID.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // Create the ViewModel for the All Transactions page.
            var model = new AllTransactionsViewModel();

            // Retrieve active income categories.
            var incomeCategories = await _context.IncomeCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.IncomeCategoryId,
                    Text = "Income - " + c.CategoryName
                })
                .ToListAsync();

            // Retrieve active expense categories.
            var expenseCategories = await _context.ExpenseCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.ExpenseCategoryId,
                    Text = "Expense - " + c.CategoryName
                })
                .ToListAsync();

            // Add a default option.
            model.Categories.Add(new SelectListItem
            {
                Value = "",
                Text = "All Categories"
            });

            // Add all available categories.
            model.Categories.AddRange(incomeCategories);
            model.Categories.AddRange(expenseCategories);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Filter(
    [FromQuery] TransactionFilterViewModel filter)
        {
            // 1. Get the authenticated user.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // 2. Validate filter values.
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid filter values.");
            }

            string transactionType =
                filter.TransactionType ?? "All";

            string sortBy = filter.SortBy ?? "Newest";

            if (transactionType != "All" &&
                transactionType != "Income" &&
                transactionType != "Expense")
            {
                return BadRequest("Invalid transaction type.");
            }

            if (sortBy != "Newest" &&
                sortBy != "Oldest" &&
                sortBy != "HighestAmount" &&
                sortBy != "LowestAmount")
            {
                return BadRequest("Invalid sorting option.");
            }

            if (filter.StartDate.HasValue &&
                filter.EndDate.HasValue &&
                filter.StartDate.Value.Date >
                filter.EndDate.Value.Date)
            {
                return BadRequest(
                    "Start date cannot be later than end date.");
            }

            if (filter.MinAmount.HasValue &&
                filter.MaxAmount.HasValue &&
                filter.MinAmount.Value > filter.MaxAmount.Value)
            {
                return BadRequest(
                    "Minimum amount cannot exceed maximum amount.");
            }

            // Prevent invalid or excessive pagination requests.
            int page = Math.Max(1, filter.Page);

            int pageSize = filter.PageSize switch
            {
                10 => 10,
                25 => 25,
                50 => 50,
                _ => 10
            };

            string search = filter.Search?.Trim() ?? "";

            // 3. Start income query.
            var incomeQuery =
                from income in _context.Incomes.AsNoTracking()
                join category in _context.IncomeCategories.AsNoTracking()
                    on income.IncomeCategoryId
                    equals category.IncomeCategoryId
                where income.UserId == userId
                select new
                {
                    TransactionId = income.IncomeId,
                    TransactionType = "Income",
                    CategoryId = income.IncomeCategoryId,
                    CategoryName = category.CategoryName,
                    Amount = income.Amount,
                    TransactionDate = income.IncomeDate,
                    Description = income.Description,
                    HasReceipt = false
                };

            // 4. Start expense query.
            var expenseQuery =
                from expense in _context.Expenses.AsNoTracking()
                join category in _context.ExpenseCategories.AsNoTracking()
                    on expense.ExpenseCategoryId
                    equals category.ExpenseCategoryId
                where expense.UserId == userId
                select new
                {
                    TransactionId = expense.ExpenseId,
                    TransactionType = "Expense",
                    CategoryId = expense.ExpenseCategoryId,
                    CategoryName = category.CategoryName,
                    Amount = expense.Amount,
                    TransactionDate = expense.ExpenseDate,
                    Description = expense.Description,
                    HasReceipt =
                        expense.ReceiptPath != null &&
                        expense.ReceiptPath != ""
                };

            // 5. Combine income and expenses.
            var query = incomeQuery.Concat(expenseQuery);

            // 6. Filter transaction type.
            if (transactionType != "All")
            {
                query = query.Where(t =>
                    t.TransactionType == transactionType);
            }

            // 7. Filter category.
            if (!string.IsNullOrWhiteSpace(filter.CategoryId))
            {
                query = query.Where(t =>
                    t.CategoryId == filter.CategoryId);
            }

            // 8. Search transaction ID, category or description.
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t =>
                    t.TransactionId.Contains(search) ||
                    t.CategoryName.Contains(search) ||
                    (t.Description != null &&
                     t.Description.Contains(search)));
            }

            // 9. Filter start date.
            if (filter.StartDate.HasValue)
            {
                DateTime startDate =
                    filter.StartDate.Value.Date;

                query = query.Where(t =>
                    t.TransactionDate >= startDate);
            }

            // 10. Filter end date (inclusive).
            if (filter.EndDate.HasValue)
            {
                DateTime endExclusive =
                    filter.EndDate.Value.Date.AddDays(1);

                query = query.Where(t =>
                    t.TransactionDate < endExclusive);
            }

            // 11. Filter minimum amount.
            if (filter.MinAmount.HasValue)
            {
                decimal minAmount = filter.MinAmount.Value;

                query = query.Where(t =>
                    t.Amount >= minAmount);
            }

            // 12. Filter maximum amount.
            if (filter.MaxAmount.HasValue)
            {
                decimal maxAmount = filter.MaxAmount.Value;

                query = query.Where(t =>
                    t.Amount <= maxAmount);
            }

            // 13. Count all matching records before pagination.
            int totalRecords = await query.CountAsync();

            // 14. Calculate pagination.
            int totalPages = (int)Math.Ceiling(
                (double)totalRecords / pageSize);

            if (totalPages > 0)
            {
                page = Math.Min(page, totalPages);
            }
            else
            {
                page = 1;
            }

            // 15. Apply sorting.
            var sortedQuery = sortBy switch
            {
                "Oldest" => query
                    .OrderBy(t => t.TransactionDate)
                    .ThenBy(t => t.TransactionId),

                "HighestAmount" => query
                    .OrderByDescending(t => t.Amount)
                    .ThenByDescending(t => t.TransactionDate)
                    .ThenBy(t => t.TransactionId),

                "LowestAmount" => query
                    .OrderBy(t => t.Amount)
                    .ThenByDescending(t => t.TransactionDate)
                    .ThenBy(t => t.TransactionId),

                _ => query
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenBy(t => t.TransactionId)
            };

            // 16. Retrieve only the requested page.
            var transactions = await sortedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionViewModel
                {
                    TransactionId = t.TransactionId,
                    TransactionType = t.TransactionType,
                    CategoryName = t.CategoryName,
                    Amount = t.Amount,
                    TransactionDate = t.TransactionDate,
                    Description = t.Description,
                    HasReceipt = t.HasReceipt
                })
                .ToListAsync();

            // 17. Prepare the partial view model.
            var model = new AllTransactionsViewModel
            {
                Filter = new TransactionFilterViewModel
                {
                    Search = filter.Search,
                    TransactionType = transactionType,
                    CategoryId = filter.CategoryId,
                    StartDate = filter.StartDate,
                    EndDate = filter.EndDate,
                    MinAmount = filter.MinAmount,
                    MaxAmount = filter.MaxAmount,
                    SortBy = sortBy,
                    Page = page,
                    PageSize = pageSize
                },

                Transactions = transactions,
                TotalRecords = totalRecords
            };

            // 18. Return only the table HTML for AJAX updates.
            return PartialView("_TransactionTable", model);
        }
    }
}