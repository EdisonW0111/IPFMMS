using System.Globalization;

namespace IPFMMS.Services
{
    public static class BudgetRepeatUntil
    {
        public static bool TryParse(bool repeatMonthly, string? value, DateTime budgetMonth,
            out DateTime? endMonth, out string? error)
        {
            endMonth = null;
            error = null;
            if (!repeatMonthly || string.IsNullOrWhiteSpace(value)) return true;
            if (!DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) || parsed.Year < 1000 || parsed.Year > 9998)
            {
                error = "Please select a valid repeat-until month.";
                return false;
            }
            endMonth = new DateTime(parsed.Year, parsed.Month, 1);
            if (endMonth < budgetMonth)
            {
                error = "Repeat until cannot be before the budget month.";
                return false;
            }
            return true;
        }
    }
}
