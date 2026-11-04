namespace IPFMMS.Services
{
    public static class SavingsTargetCalculator
    {
        public static decimal? Calculate(decimal? targetAmount, DateTime? targetDate, DateTime today)
        {
            if (targetAmount is not decimal amount || amount < 0.01m || amount > 9999999999.99m ||
                decimal.Round(amount, 2) != amount || !targetDate.HasValue ||
                targetDate.Value.Date < today.Date || targetDate.Value.Year > 9998)
                return null;

            // One saving opportunity in each calendar month, including the
            // current month and the target month. A same-month goal uses one.
            int months = (targetDate.Value.Year - today.Year) * 12 + targetDate.Value.Month - today.Month + 1;
            return decimal.Ceiling(amount * 100m / months) / 100m;
        }
    }
}
