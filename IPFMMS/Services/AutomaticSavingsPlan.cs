namespace IPFMMS.Services;
public static class AutomaticSavingsPlan
{
    public static DateTime FirstDue(DateTime today, int day)
    {
        var due = new DateTime(today.Year, today.Month, day);
        return due < today.Date ? due.AddMonths(1) : due;
    }
    public static decimal? Monthly(decimal? remaining, DateTime? target, DateTime today, int? day)
    {
        if (day is not (>= 1 and <= 28) || !target.HasValue || target.Value.Year > 9998 || remaining is null or < 0m) return null;
        if (remaining == 0m) return 0m;
        var first = FirstDue(today, day.Value);
        if (first > target.Value.Date) return null;
        int count = (target.Value.Year-first.Year)*12+target.Value.Month-first.Month+1;
        if (target.Value.Day < day) count--;
        return count > 0 ? decimal.Ceiling(remaining.Value*100m/count)/100m : null;
    }
    public static (decimal Credit, decimal Deduction) Calculate(decimal planned, decimal remaining, decimal saved,
        decimal baseline, decimal paidAutomatically, decimal usedCredit)
    {
        decimal credit = Math.Min(Math.Max(0m, saved-baseline-paidAutomatically-usedCredit), planned);
        return (credit, Math.Min(Math.Max(0m, remaining), Math.Max(0m, planned-credit)));
    }
}
