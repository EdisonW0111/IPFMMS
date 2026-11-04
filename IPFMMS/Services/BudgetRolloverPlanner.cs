using IPFMMS.Models;

namespace IPFMMS.Services
{
    // Decide what to create before changing tracked entities or generating IDs.
    public static class BudgetRolloverPlanner
    {
        public sealed class Plan
        {
            public List<Budget> Budgets { get; } = new();
            public List<BudgetSchedule> SchedulesToAdvance { get; } = new();
            public List<string> Warnings { get; } = new();
        }

        public static Plan Build(string userId, DateTime month,
            IEnumerable<BudgetSchedule> schedules, IEnumerable<Budget> budgets)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("An owner is required.");
            if (month.Day != 1 || month.TimeOfDay != TimeSpan.Zero || month.Year < 1000 || month.Year > 9998)
                throw new ArgumentException("A valid first day of the month is required.");

            var plan = new Plan();
            var existing = budgets.Where(b => b.UserId == userId && b.BudgetMonth == month).ToList();
            var categories = existing.Select(b => b.ExpenseCategoryId).ToHashSet();
            var pending = new List<BudgetSchedule>();

            foreach (var schedule in schedules.Where(s => s.UserId == userId &&
                s.IsActive && s.NextBudgetMonth <= month &&
                (!s.EndMonth.HasValue || month <= s.EndMonth.Value)).OrderBy(s => s.ExpenseCategoryId))
            {
                if (categories.Contains(schedule.ExpenseCategoryId))
                {
                    // An existing manual budget takes precedence over the schedule.
                    plan.SchedulesToAdvance.Add(schedule);
                    continue;
                }

                string name = schedule.ExpenseCategory?.CategoryName ?? schedule.ExpenseCategoryId;
                if (schedule.ExpenseCategory?.IsActive != true)
                {
                    plan.Warnings.Add($"{name}: repetition was skipped because the category is inactive.");
                    continue;
                }

                bool valid = schedule.BudgetType switch
                {
                    "FixedAmount" => schedule.BudgetAmount is decimal amount && amount >= 0.01m &&
                        amount <= 9999999999.99m && decimal.Round(amount, 2) == amount,
                    "Percentage" => schedule.BudgetPercentage is decimal percentage && percentage >= 1m &&
                        percentage <= 100m && decimal.Round(percentage, 2) == percentage,
                    _ => false
                };
                if (!valid)
                {
                    plan.Warnings.Add($"{name}: repetition was skipped because its settings are invalid.");
                    continue;
                }
                pending.Add(schedule);
            }

            decimal existingPercentage = existing.Where(b => b.BudgetType == "Percentage")
                .Sum(b => b.BudgetPercentage ?? 0m);
            decimal pendingPercentage = pending.Where(s => s.BudgetType == "Percentage")
                .Sum(s => s.BudgetPercentage ?? 0m);
            bool percentageConflict = existingPercentage + pendingPercentage > 100m;
            if (percentageConflict && pendingPercentage > 0m)
                plan.Warnings.Add("Repeating percentage budgets were not created for this month because " +
                    "they would exceed the 100% allocation limit. Review the existing monthly budgets and repeat settings.");

            foreach (var schedule in pending)
            {
                if (schedule.BudgetType == "Percentage" && percentageConflict) continue;
                plan.Budgets.Add(new Budget
                {
                    UserId = userId,
                    ExpenseCategoryId = schedule.ExpenseCategoryId,
                    BudgetMonth = month,
                    BudgetType = schedule.BudgetType,
                    BudgetAmount = schedule.BudgetType == "FixedAmount" ? schedule.BudgetAmount : null,
                    BudgetPercentage = schedule.BudgetType == "Percentage" ? schedule.BudgetPercentage : null,
                    Notes = schedule.Notes,
                    CreatedDate = DateTime.UtcNow
                });
                plan.SchedulesToAdvance.Add(schedule);
            }
            return plan;
        }
    }
}
