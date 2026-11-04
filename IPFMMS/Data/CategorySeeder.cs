using IPFMMS.Models;
using IPFMMS.Services;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Data
{
    public static class CategorySeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext context,
            IdGeneratorService idGenerator)
        {
            // Default income categories.
            string[] incomeNames =
            {
                "Salary",
                "Allowance",
                "Scholarship",
                "Part-Time Job",
                "Freelance",
                "Investment Returns",
                "Gifts",
                "Other Income"
            };

            // Default expense categories.
            string[] expenseNames =
            {
                "Food & Drinks",
                "Transportation",
                "Education",
                "Shopping",
                "Entertainment",
                "Bills & Utilities",
                "Healthcare",
                "Loan Interest & Fees",
                "Other Expenses"
            };

            // Seed income categories.
            var existingIncomeNames =
                await context.IncomeCategories
                    .Select(c => c.CategoryName)
                    .ToListAsync();

            var existingIncomeIds =
                await context.IncomeCategories
                    .Select(c => c.IncomeCategoryId)
                    .ToListAsync();

            int incomeNumber = existingIncomeIds
                .Select(id => int.TryParse(
                    id.Replace("ICG", ""), out int n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();

            foreach (string name in incomeNames)
            {
                if (existingIncomeNames.Any(n =>
                    string.Equals(
                        n,
                        name,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string categoryId = await idGenerator.GenerateIdAsync("ICG");

                context.IncomeCategories.Add(
                    new IncomeCategory
                    {
                        IncomeCategoryId = categoryId,
                        CategoryName = name,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    });
            }

            // Seed expense categories.
            var existingExpenseNames =
                await context.ExpenseCategories
                    .Select(c => c.CategoryName)
                    .ToListAsync();

            var existingExpenseIds =
                await context.ExpenseCategories
                    .Select(c => c.ExpenseCategoryId)
                    .ToListAsync();

            int expenseNumber = existingExpenseIds
                .Select(id => int.TryParse(
                    id.Replace("ECG", ""), out int n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();

            foreach (string name in expenseNames)
            {
                if (existingExpenseNames.Any(n =>
                    string.Equals(
                        n,
                        name,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string categoryId = await idGenerator.GenerateIdAsync("ECG");

                context.ExpenseCategories.Add(
                    new ExpenseCategory
                    {
                        ExpenseCategoryId = categoryId,
                        CategoryName = name,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    });

                existingExpenseNames.Add(name);
            }

            await context.SaveChangesAsync();
        }
    }
}
