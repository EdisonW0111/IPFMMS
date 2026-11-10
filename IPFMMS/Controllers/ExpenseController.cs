using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Azure;
using Azure.Storage.Blobs.Models;

namespace IPFMMS.Controllers
{
    [Authorize]
    public class ExpenseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IdGeneratorService _idGenerator;
        private readonly IReceiptStorageService _receiptStorage;
        private readonly ILogger<ExpenseController> _logger;

        private const long MaxReceiptSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedPaymentMethods =
        {
            "Cash",
            "Debit Card",
            "Credit Card",
            "E-Wallet",
            "Bank Transfer",
            "Other"
        };

        public ExpenseController(
            ApplicationDbContext context,
            IdGeneratorService idGenerator,
            IReceiptStorageService receiptStorage,
            ILogger<ExpenseController> logger)
        {
            _context = context;
            _idGenerator = idGenerator;
            _receiptStorage = receiptStorage;
            _logger = logger;
        }

        // GET: /Expense/Add
        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var model = new AddExpenseViewModel();

            await LoadDropdownsAsync(model);

            return View(model);
        }

        // POST: /Expense/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Add(
            AddExpenseViewModel model)
        {
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            // Validate expense date.
            if (model.ExpenseDate.HasValue &&
                model.ExpenseDate.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(model.ExpenseDate),
                    "Expense date cannot be in the future.");
            }

            // Validate category.
            bool categoryExists =
                await _context.ExpenseCategories
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

            // Validate payment method.
            if (!AllowedPaymentMethods.Contains(model.PaymentMethod))
            {
                ModelState.AddModelError(
                    nameof(model.PaymentMethod),
                    "Please select a valid payment method.");
            }

            // Validate receipt before saving anything.
            if (model.ReceiptFile != null)
            {
                if (model.ReceiptFile.Length == 0 ||
                    model.ReceiptFile.Length > MaxReceiptSize)
                {
                    ModelState.AddModelError(
                        nameof(model.ReceiptFile),
                        "Receipt must be between 1 byte and 5 MB.");
                }
                else if (!IsValidReceipt(model.ReceiptFile))
                {
                    ModelState.AddModelError(
                        nameof(model.ReceiptFile),
                        "Only valid PDF, JPG or PNG receipts are allowed.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(model);
                return View(model);
            }

            string? receiptPath = null;

            try
            {
                // Upload the validated receipt to Azure Blob Storage.
                if (model.ReceiptFile != null)
                {
                    receiptPath = await _receiptStorage.UploadAsync(
                        model.ReceiptFile,
                        HttpContext.RequestAborted);
                }

                // Generate a new expense ID.
                string expenseId =
                    await _idGenerator.GenerateIdAsync("EXP");

                var expense = new Expense
                {
                    ExpenseId = expenseId,
                    UserId = userId,
                    ExpenseCategoryId = model.ExpenseCategoryId,
                    Amount = model.Amount!.Value,
                    ExpenseDate = model.ExpenseDate!.Value.Date,
                    PaymentMethod = model.PaymentMethod,
                    Description = model.Description?.Trim(),
                    ReceiptPath = receiptPath,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Expenses.Add(expense);

                await _context.SaveChangesAsync();
            }
            catch
            {
                // If saving the expense fails, attempt to remove
                // the receipt that was already uploaded.
                if (!string.IsNullOrWhiteSpace(receiptPath))
                {
                    try
                    {
                        await _receiptStorage.DeleteAsync(
                            receiptPath,
                            CancellationToken.None);
                    }
                    catch (Exception cleanupException)
                    {
                        // Cleanup failure is logged separately so
                        // the original error is not hidden.
                        _logger.LogError(
                            cleanupException,
                            "Failed to clean up uploaded receipt.");
                    }
                }

                throw;
            }

            TempData["SuccessMessage"] =
                "Expense added successfully.";

            return RedirectToAction(
                "Index", "Transaction");
        }

        private async Task LoadDropdownsAsync(
            AddExpenseViewModel model)
        {
            model.ExpenseCategories =
                await _context.ExpenseCategories
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

            model.PaymentMethods =
                AllowedPaymentMethods
                    .Select(method => new SelectListItem
                    {
                        Value = method,
                        Text = method
                    })
                    .ToList();
        }

        // Detect supported file types using file signatures.
        private static bool IsValidReceipt(IFormFile file)
        {
            string extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            if (extension != ".pdf" &&
                extension != ".jpg" &&
                extension != ".jpeg" &&
                extension != ".png")
            {
                return false;
            }

            byte[] header = new byte[8];

            using var stream = file.OpenReadStream();
            int bytesRead = stream.Read(header, 0, header.Length);

            if (extension == ".pdf")
            {
                return bytesRead >= 5 &&
                    header[0] == 0x25 &&
                    header[1] == 0x50 &&
                    header[2] == 0x44 &&
                    header[3] == 0x46 &&
                    header[4] == 0x2D;
            }

            if (extension == ".png")
            {
                byte[] pngSignature =
                {
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A
                };

                return bytesRead >= 8 &&
                    header.SequenceEqual(pngSignature);
            }

            // JPG/JPEG signature.
            return bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF;
        }

        private static string GetReceiptExtension(
            IFormFile file)
        {
            string extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            return extension == ".jpeg" ? ".jpg" : extension;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ViewReceipt(string expenseId)
        {
            // 1. Check the expense ID.
            if (string.IsNullOrWhiteSpace(expenseId))
            {
                return NotFound();
            }

            // 2. Get the currently logged-in user.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // 3. Find the expense belonging to this user.
            var expense = await _context.Expenses
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.ExpenseId == expenseId &&
                    e.UserId == userId);

            // 4. Do not reveal receipts belonging to other users.
            if (expense == null ||
                string.IsNullOrWhiteSpace(expense.ReceiptPath))
            {
                return NotFound();
            }

            try
            {
                // 5. Download the receipt from private Azure Blob Storage.
                var (content, contentType) =
                    await _receiptStorage.DownloadAsync(
                        expense.ReceiptPath,
                        HttpContext.RequestAborted);

                // 6. Restrict the MIME types that may be rendered.
                string safeContentType = contentType switch
                {
                    "image/jpeg" => "image/jpeg",
                    "image/png" => "image/png",
                    "application/pdf" => "application/pdf",
                    _ => "application/octet-stream"
                };

                // 7. Prevent the browser from guessing the file type.
                Response.Headers["X-Content-Type-Options"] = "nosniff";

                // 8. Prevent caching sensitive financial receipts.
                Response.Headers["Cache-Control"] = "private, no-store";

                // 9. Return the file to the authenticated user.
                return File(content, safeContentType);
            }
            catch (RequestFailedException ex)
                when (ex.Status == 404)
            {
                // Receipt record exists but its blob is missing.
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            // 1. Get the currently logged-in user.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // 2. Retrieve only the user's own expense.
            var expense = await _context.Expenses
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.ExpenseId == id &&
                    e.UserId == userId);

            if (expense == null)
            {
                return NotFound();
            }

            if(await _context.SharedFinanceTransactions.AnyAsync(t=>t.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is linked to a shared finance entry and is read-only to preserve its history.";
                return RedirectToAction("PersonalEntry","SharedFinance",new{expenseId=expense.ExpenseId});
            }
            if(await _context.DebtRepayments.AnyAsync(p=>p.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is linked to a debt repayment and is read-only to preserve its history.";
                return RedirectToAction("Payment","Debt",new{expenseId=expense.ExpenseId});
            }
            if(await _context.RoutineBillPayments.AnyAsync(p=>p.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is a recorded bill payment. View it in Routine Bills payment history.";
                return RedirectToAction("History","RoutineBills");
            }

            // 3. Populate the Edit Expense form.
            var model = new EditExpenseViewModel
            {
                ExpenseId = expense.ExpenseId,
                ExpenseCategoryId = expense.ExpenseCategoryId,
                Amount = expense.Amount,
                ExpenseDate = expense.ExpenseDate,
                PaymentMethod = expense.PaymentMethod,
                Description = expense.Description,

                HasExistingReceipt =
                    !string.IsNullOrWhiteSpace(expense.ReceiptPath),

                RemoveReceipt = false
            };

            // 4. Load available expense categories.
            // Include the current category even if it is inactive.
            model.ExpenseCategories = await _context.ExpenseCategories
                .AsNoTracking()
                .Where(c =>
                    c.IsActive ||
                    c.ExpenseCategoryId == expense.ExpenseCategoryId)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.ExpenseCategoryId,
                    Text = c.CategoryName
                })
                .ToListAsync();

            // 5. Load payment methods.
            model.PaymentMethods = new List<SelectListItem>
    {
        new SelectListItem
        {
            Value = "Cash",
            Text = "Cash"
        },
        new SelectListItem
        {
            Value = "Debit Card",
            Text = "Debit Card"
        },
        new SelectListItem
        {
            Value = "Credit Card",
            Text = "Credit Card"
        },
        new SelectListItem
        {
            Value = "E-Wallet",
            Text = "E-Wallet"
        },
        new SelectListItem
        {
            Value = "Bank Transfer",
            Text = "Bank Transfer"
        },
        new SelectListItem
        {
            Value = "Other",
            Text = "Other"
        }
    };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditExpenseViewModel model)
        {
            // 1. Identify the authenticated user.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // 2. Retrieve only the user's own expense.
            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e =>
                    e.ExpenseId == model.ExpenseId &&
                    e.UserId == userId);

            if (expense == null)
            {
                return NotFound();
            }

            if(await _context.SharedFinanceTransactions.AnyAsync(t=>t.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is linked to a shared finance entry and is read-only to preserve its history.";
                return RedirectToAction("PersonalEntry","SharedFinance",new{expenseId=expense.ExpenseId});
            }
            if(await _context.DebtRepayments.AnyAsync(p=>p.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is linked to a debt repayment and is read-only to preserve its history.";
                return RedirectToAction("Payment","Debt",new{expenseId=expense.ExpenseId});
            }
            if(await _context.RoutineBillPayments.AnyAsync(p=>p.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is a recorded bill payment. View it in Routine Bills payment history.";
                return RedirectToAction("History","RoutineBills");
            }

            // 3. Read existing receipt information from MySQL.
            string? oldReceiptPath = expense.ReceiptPath;

            model.HasExistingReceipt =
                !string.IsNullOrWhiteSpace(oldReceiptPath);

            // 4. Validate expense date.
            if (model.ExpenseDate.HasValue &&
                model.ExpenseDate.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(model.ExpenseDate),
                    "Expense date cannot be in the future.");
            }

            // 5. Validate expense category.
            bool categoryExists = await _context.ExpenseCategories
                .AnyAsync(c =>
                    c.ExpenseCategoryId == model.ExpenseCategoryId &&
                    (c.IsActive ||
                     c.ExpenseCategoryId == expense.ExpenseCategoryId));

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(model.ExpenseCategoryId),
                    "Please select a valid expense category.");
            }

            // 6. Validate payment method.
            string[] allowedPaymentMethods =
            {
        "Cash",
        "Debit Card",
        "Credit Card",
        "E-Wallet",
        "Bank Transfer",
        "Other"
    };

            if (!allowedPaymentMethods.Contains(model.PaymentMethod))
            {
                ModelState.AddModelError(
                    nameof(model.PaymentMethod),
                    "Please select a valid payment method.");
            }

            // 7. Prevent conflicting receipt operations.
            if (model.RemoveReceipt && model.ReceiptFile != null)
            {
                ModelState.AddModelError(
                    nameof(model.ReceiptFile),
                    "You cannot upload and remove a receipt at the same time.");
            }

            // 8. Validate an uploaded receipt.
            if (model.ReceiptFile != null)
            {
                bool validReceipt = await IsValidEditReceiptAsync(
                    model.ReceiptFile);

                if (!validReceipt)
                {
                    ModelState.AddModelError(
                        nameof(model.ReceiptFile),
                        "Please upload a valid JPG, PNG or PDF file (maximum 5 MB).");
                }
            }

            // 9. Reload dropdowns when validation fails.
            if (!ModelState.IsValid)
            {
                model.ExpenseCategories =
                    await _context.ExpenseCategories
                        .AsNoTracking()
                        .Where(c =>
                            c.IsActive ||
                            c.ExpenseCategoryId == expense.ExpenseCategoryId)
                        .OrderBy(c => c.DisplayOrder)
                        .ThenBy(c => c.CategoryName)
                        .Select(c => new SelectListItem
                        {
                            Value = c.ExpenseCategoryId,
                            Text = c.CategoryName
                        })
                        .ToListAsync();

                model.PaymentMethods = allowedPaymentMethods
                    .Select(method => new SelectListItem
                    {
                        Value = method,
                        Text = method
                    })
                    .ToList();

                return View(model);
            }

            // 10. Track any newly uploaded receipt.
            string? newReceiptPath = null;

            try
            {
                // 11. Upload replacement receipt first.
                if (model.ReceiptFile != null)
                {
                    newReceiptPath =
                        await _receiptStorage.UploadAsync(
                            model.ReceiptFile,
                            HttpContext.RequestAborted);
                }

                // 12. Update expense details.
                expense.ExpenseCategoryId = model.ExpenseCategoryId;
                expense.Amount = model.Amount!.Value;
                expense.ExpenseDate = model.ExpenseDate!.Value.Date;
                expense.PaymentMethod = model.PaymentMethod;
                expense.Description = model.Description?.Trim();

                // 13. Determine which receipt to keep.
                if (newReceiptPath != null)
                {
                    // Replace the existing receipt.
                    expense.ReceiptPath = newReceiptPath;
                }
                else if (model.RemoveReceipt)
                {
                    // Remove the existing receipt reference.
                    expense.ReceiptPath = null;
                }

                // Otherwise keep the existing receipt.

                // 14. Save changes to MySQL.
                await _context.SaveChangesAsync();
            }
            catch
            {
                // If the update fails after uploading a new file,
                // attempt to delete that unused file.
                if (!string.IsNullOrWhiteSpace(newReceiptPath))
                {
                    try
                    {
                        await _receiptStorage.DeleteAsync(
                            newReceiptPath,
                            CancellationToken.None);
                    }
                    catch (Exception cleanupException)
                    {
                        _logger.LogError(
                            cleanupException,
                            "Failed to clean up replacement receipt.");
                    }
                }

                throw;
            }

            // 15. Delete the old receipt only after MySQL succeeds.
            bool shouldDeleteOldReceipt =
                !string.IsNullOrWhiteSpace(oldReceiptPath) &&
                (model.RemoveReceipt || newReceiptPath != null);

            if (shouldDeleteOldReceipt)
            {
                try
                {
                    await _receiptStorage.DeleteAsync(
                        oldReceiptPath!,
                        CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    // The expense was saved successfully.
                    // Log cleanup failure without undoing the update.
                    _logger.LogError(
                        cleanupException,
                        "Failed to delete old expense receipt for {ExpenseId}.",
                        expense.ExpenseId);
                }
            }

            // 16. Display success message.
            TempData["SuccessMessage"] =
                "Expense transaction updated successfully.";

            // 17. Return to Transactions overview.
            return RedirectToAction("Index", "Transaction");
        }

        private static async Task<bool> IsValidEditReceiptAsync(
    IFormFile file)
        {
            const long maxFileSize = 5 * 1024 * 1024;

            if (file.Length <= 0 || file.Length > maxFileSize)
            {
                return false;
            }

            string extension = Path.GetExtension(
                file.FileName).ToLowerInvariant();

            if (extension != ".jpg" &&
                extension != ".jpeg" &&
                extension != ".png" &&
                extension != ".pdf")
            {
                return false;
            }

            byte[] header = new byte[8];

            using var stream = file.OpenReadStream();

            int bytesRead = 0;

            while (bytesRead < header.Length)
            {
                int count = await stream.ReadAsync(
                    header.AsMemory(
                        bytesRead,
                        header.Length - bytesRead));

                if (count == 0)
                {
                    break;
                }

                bytesRead += count;
            }

            // JPEG: FF D8 FF
            bool isJpeg =
                bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            byte[] pngSignature =
            {
        0x89, 0x50, 0x4E, 0x47,
        0x0D, 0x0A, 0x1A, 0x0A
    };

            bool isPng =
                bytesRead >= 8 &&
                header.SequenceEqual(pngSignature);

            // PDF: %PDF-
            bool isPdf =
                bytesRead >= 5 &&
                header[0] == 0x25 &&
                header[1] == 0x50 &&
                header[2] == 0x44 &&
                header[3] == 0x46 &&
                header[4] == 0x2D;

            return extension switch
            {
                ".jpg" or ".jpeg" => isJpeg,
                ".png" => isPng,
                ".pdf" => isPdf,
                _ => false
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            // 1. Identify the authenticated user.
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // 2. Retrieve only the user's own expense.
            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e =>
                    e.ExpenseId == id &&
                    e.UserId == userId);

            if (expense == null)
            {
                return NotFound();
            }

            if(await _context.SharedFinanceTransactions.AnyAsync(t=>t.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is linked to a shared finance entry and is read-only to preserve its history.";
                return RedirectToAction("PersonalEntry","SharedFinance",new{expenseId=expense.ExpenseId});
            }
            if(await _context.DebtRepayments.AnyAsync(p=>p.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is linked to a debt repayment and is read-only to preserve its history.";
                return RedirectToAction("Payment","Debt",new{expenseId=expense.ExpenseId});
            }
            if(await _context.RoutineBillPayments.AnyAsync(p=>p.expenseId==expense.ExpenseId))
            {
                TempData["WarningMessage"]="This expense is a recorded bill payment. View it in Routine Bills payment history.";
                return RedirectToAction("History","RoutineBills");
            }

            // 3. Remember the receipt before deleting the record.
            string? receiptPath = expense.ReceiptPath;

            // 4. Delete the expense from MySQL.
            _context.Expenses.Remove(expense);

            await _context.SaveChangesAsync();

            // 5. Delete the receipt from Azure Blob Storage
            // only after the database deletion succeeds.
            bool receiptCleanupFailed = false;

            if (!string.IsNullOrWhiteSpace(receiptPath))
            {
                try
                {
                    await _receiptStorage.DeleteAsync(
                        receiptPath,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    receiptCleanupFailed = true;

                    _logger.LogError(
                        ex,
                        "Failed to delete receipt for expense {ExpenseId}.",
                        id);
                }
            }

            // 6. Display the result.
            TempData["SuccessMessage"] = receiptCleanupFailed
                ? "Expense deleted, but receipt storage cleanup failed."
                : "Expense transaction deleted successfully.";

            // 7. Redirect to Transactions.
            return RedirectToAction("Index", "Transaction");
        }
    }
}