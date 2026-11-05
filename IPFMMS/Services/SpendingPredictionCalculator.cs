using IPFMMS.ViewModels;
namespace IPFMMS.Services;
public static class SpendingPredictionCalculator
{
    public static void CalculateProvisional(SpendingPredictionViewModel model, DateTime today, decimal monthToDate, int recordCount)
    {
        var current = new DateTime(today.Year, today.Month, 1);
        if (model.Month != current.AddMonths(1) || model.Months.Count != 3 || monthToDate < 0 || recordCount < 0)
            throw new ArgumentException("A provisional prediction must target next month with three source months.");
        model.ProjectionAsOf = today.Date;
        model.MonthToDateSpending = monthToDate;
        model.ElapsedDays = today.Day;
        model.DaysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var latest = model.Months[2];
        latest.Month = current;
        latest.Count = recordCount;
        // Retain full decimal precision until the final weighted total is rounded.
        latest.Amount = monthToDate / model.ElapsedDays * model.DaysInMonth;
        Calculate(model);
    }

    public static void Calculate(SpendingPredictionViewModel model)
    {
        model.PredictedAmount = null;
        model.Trend = "Unavailable";
        if (model.Months.Count != 3) return;
        for (var i = 0; i < 3; i++)
        {
            var entry = model.Months[i];
            entry.Weight = i == 0 ? .2m : i == 1 ? .3m : .5m;
        }
        if (model.Months.Where((entry, i) => entry.Month != model.Month.AddMonths(i - 3) || entry.Count < 1 || entry.Amount < 0).Any()) return;
        model.PredictedAmount = decimal.Round(model.Months.Sum(m => m.WeightedAmount), 2, MidpointRounding.AwayFromZero);
        var latest = decimal.Round(model.Months[2].Amount, 2, MidpointRounding.AwayFromZero);
        model.Trend = model.PredictedAmount > latest ? "Increasing" : model.PredictedAmount < latest ? "Decreasing" : "Stable";
    }
}
