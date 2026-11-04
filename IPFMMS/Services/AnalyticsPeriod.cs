using System.Globalization;

namespace IPFMMS.Services;

public static class AnalyticsPeriod
{
    public static bool TryParse(string? from, string? to, DateTime today,
        out DateTime start, out DateTime end, out string? error)
    {
        var current = new DateTime(today.Year, today.Month, 1);
        start = current.AddMonths(-5);
        end = current;
        error = null;
        if (from != null && !ParseMonth(from, out start) || to != null && !ParseMonth(to, out end))
            error = "Choose valid months between January 1000 and December 9998.";
        else if (start > end)
            error = "The start month must be on or before the end month.";
        else if ((end.Year - start.Year) * 12 + end.Month - start.Month >= 12)
            error = "Choose a period of up to 12 months.";
        return error == null;
    }

    private static bool ParseMonth(string value, out DateTime month) =>
        DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month) &&
        month.Year is >= 1000 and <= 9998;
}
