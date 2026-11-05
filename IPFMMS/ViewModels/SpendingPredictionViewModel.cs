using IPFMMS.Models;
namespace IPFMMS.ViewModels;
public sealed class SpendingPredictionViewModel
{
    public SpendingPredictionViewModel? NextMonthPreview { get; set; }
    public DateTime? ProjectionAsOf { get; set; }
    public decimal MonthToDateSpending { get; set; }
    public int ElapsedDays { get; set; }
    public int DaysInMonth { get; set; }
    public DateTime Month { get; set; }
    public List<PredictionBaselineMonth> Months { get; set; } = new();
    public decimal? PredictedAmount { get; set; }
    public string Trend { get; set; } = "Unavailable";
    public decimal? Difference => PredictedAmount - Months.LastOrDefault()?.Amount;
    public decimal? ChangePercent => Months.LastOrDefault()?.Amount > 0 ? Difference / Months.Last().Amount * 100m : null;
    public SpendingPrediction? SavedRecord { get; set; }
    public List<SpendingPrediction> History { get; set; } = new();
    public int HistoryCount { get; set; }
    public int HistoryPage { get; set; } = 1;
    public int HistoryPages => Math.Max(1, (HistoryCount + 11) / 12);
}
public sealed class PredictionBaselineMonth
{
    public DateTime Month { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Weight { get; set; }
    public decimal WeightedAmount => Amount * Weight;
}
