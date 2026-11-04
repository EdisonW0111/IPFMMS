using IPFMMS.Models;

namespace IPFMMS.Services
{
    // Build every due month chronologically before changing schedule settings.
    public static class BudgetRolloverHistoryPlanner
    {
        public sealed class Plan
        {
            public List<Budget> Budgets { get; } = new();
            public Dictionary<BudgetSchedule, DateTime> NextMonths { get; } = new();
            public List<string> Warnings { get; } = new();
        }

        public static Plan Build(string userId, DateTime throughMonth,
            IEnumerable<BudgetSchedule> schedules, IEnumerable<Budget> budgets, IEnumerable<BudgetRepeatSkip>? skippedMonths = null)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("An owner is required.");
            if (throughMonth.Year < 1000 || throughMonth.Year > 9998 || throughMonth.Day != 1 || throughMonth.TimeOfDay != TimeSpan.Zero)
                throw new ArgumentException("A valid first day of the month is required.");

            var plan = new Plan();
            var due = schedules.Where(s => s.UserId == userId && s.IsActive && s.NextBudgetMonth <= throughMonth &&
                (!s.EndMonth.HasValue || s.NextBudgetMonth <= s.EndMonth.Value)).ToList();
            var months = budgets.Where(b => b.UserId == userId).GroupBy(b => b.BudgetMonth)
                .ToDictionary(g => g.Key, g => g.ToList());
            // Each schedule's cursor is independent. A conflict must not advance
            // past a missing month; later valid months can still be generated.
            var cursors = due.ToDictionary(s => s, s => s.NextBudgetMonth);
            if (due.Count == 0) return plan;
            var lastEligibleMonth = due.Max(s => s.EndMonth.HasValue && s.EndMonth.Value < throughMonth
                ? s.EndMonth.Value : throughMonth);
            var firstMonth = due.Min(s => s.NextBudgetMonth);
            if (firstMonth.Year < 1000 || firstMonth.Day != 1 || firstMonth.TimeOfDay != TimeSpan.Zero)
                throw new InvalidOperationException("A repeat schedule has an invalid next month.");

            // Bound work even when the next eligible month is many years old.
            var batchEnd = firstMonth.AddMonths(BudgetRolloverPolicy.CatchUpMonthsPerRequest - 1);
            var processingEnd = lastEligibleMonth < batchEnd ? lastEligibleMonth : batchEnd;
            if (processingEnd < lastEligibleMonth)
                plan.Warnings.Add($"Budget catch-up is limited to 12 months per request and processed through {processingEnd:MMMM yyyy}. " +
                    "Revisit or retry the selected month to continue. Repeat settings remain unchanged.");

            for (var month = firstMonth; month <= processingEnd; month = month.AddMonths(1))
            {
                var existing = months.GetValueOrDefault(month) ?? new List<Budget>();
                var result = BudgetRolloverPlanner.Build(userId, month, due, existing, skippedMonths);
                plan.Budgets.AddRange(result.Budgets);
                foreach (var warning in result.Warnings)
                    plan.Warnings.Add($"{month:MMMM yyyy}: {warning}");
                foreach (var schedule in result.SchedulesToAdvance)
                    if (cursors[schedule] == month)
                        cursors[schedule] = month.AddMonths(1);
            }
            foreach (var cursor in cursors)
                if (cursor.Value != cursor.Key.NextBudgetMonth)
                    plan.NextMonths.Add(cursor.Key, cursor.Value);
            return plan;
        }
    }
}
