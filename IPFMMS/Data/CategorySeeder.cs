using IPFMMS.Models;
using IPFMMS.Services;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Data
{
    public static class CategorySeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context, IdGeneratorService idGenerator)
        {
            var incomeCategories = (await context.IncomeCategories.ToListAsync())
                .ToDictionary(c => c.CategoryName, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < CategoryDefaults.IncomeNames.Count; index++)
            {
                string name = CategoryDefaults.IncomeNames[index];
                if (incomeCategories.TryGetValue(name, out var existing))
                {
                    // Existing values belong to management, including an intentional order of1000.
                    // Seeding supplies missing defaults; it never resets an existing category.
                    continue;
                }
                var category = new IncomeCategory
                {
                    IncomeCategoryId = await idGenerator.GenerateIdAsync("ICG"),
                    CategoryName = name,
                    DisplayOrder = index + 1,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                context.IncomeCategories.Add(category);
                incomeCategories.Add(name, category);
            }

            var expenseCategories = (await context.ExpenseCategories.ToListAsync())
                .ToDictionary(c => c.CategoryName, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < CategoryDefaults.ExpenseNames.Count; index++)
            {
                string name = CategoryDefaults.ExpenseNames[index];
                if (expenseCategories.TryGetValue(name, out var existing))
                {
                    // Preserve activation, name, description and administrator-assigned order.
                    continue;
                }
                var category = new ExpenseCategory
                {
                    ExpenseCategoryId = await idGenerator.GenerateIdAsync("ECG"),
                    CategoryName = name,
                    DisplayOrder = index + 1,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                context.ExpenseCategories.Add(category);
                expenseCategories.Add(name, category);
            }
            await context.SaveChangesAsync();
        }
    }
}
