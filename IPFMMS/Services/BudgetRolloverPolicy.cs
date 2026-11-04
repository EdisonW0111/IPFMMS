namespace IPFMMS.Services
{
    public static class BudgetRolloverPolicy
    {
        public const int FutureMonths = 12;
        public const int CatchUpMonthsPerRequest = 12;

        // Malaysia uses UTC+8. Use the project timezone, including on Azure.
        public static DateTime LatestMonth => GetLatestMonth(
            DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date);

        public static DateTime GetLatestMonth(DateTime today)
            => new DateTime(today.Year, today.Month, 1).AddMonths(FutureMonths);

        public static string LimitMessage(DateTime latestMonth)
            => $"Automatic budget repetition is limited to 12 months ahead (through {latestMonth:MMMM yyyy}). " +
               "Only previously saved budgets are shown beyond this limit.";
    }
}
