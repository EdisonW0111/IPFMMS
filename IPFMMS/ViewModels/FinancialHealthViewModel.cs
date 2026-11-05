using System.Globalization;
using IPFMMS.Models;
namespace IPFMMS.ViewModels;
public sealed class FinancialHealthViewModel
{
    public DateTime Month { get; set; }
    public DateTime AsOfDate { get; set; }
    public bool IsProvisional { get; set; }
    public string MonthValue => Month.ToString("yyyy-MM",CultureInfo.InvariantCulture);
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Savings { get; set; }
    public int IncomeCount { get; set; }
    public int BudgetCount { get; set; }
    public int BudgetsWithinLimit { get; set; }
    public int UnbudgetedCategories { get; set; }
    public decimal? SavingsRatio => Income>0?Savings/Income*100m:null;
    public List<HealthSpendingMonth> StabilityMonths { get; set; } = new();
    public decimal? SavingsScore { get; set; }
    public decimal? BudgetScore { get; set; }
    public decimal? StabilityScore { get; set; }
    public decimal? Overall { get; set; }
    public string Rating { get; set; } = "Not enough data";
    public List<string> MissingData { get; set; } = new();
    public List<FinancialHealthRecord> History { get; set; } = new();
    public int HistoryPage { get; set; } = 1;
    public int HistoryCount { get; set; }
    public int HistoryPages => Math.Max(1,(HistoryCount+11)/12);
    public FinancialHealthRecord? SavedRecord { get; set; }
    public decimal? SpendingVariation { get; set; }
    public bool CanSave => !IsProvisional && Overall.HasValue;
}
public sealed class HealthSpendingMonth
{
    public DateTime Month { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}
