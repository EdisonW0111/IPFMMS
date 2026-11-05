using System.Globalization;
namespace IPFMMS.ViewModels;

public sealed class SpendingInsightsViewModel
{
    public DateTime Month { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime ComparisonStart { get; set; }
    public DateTime ComparisonEnd { get; set; }
    public DateTime CurrentComparisonEnd { get; set; }
    public string MonthValue => Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public string PeriodLabel => $"{Month:dd MMM} - {PeriodEnd:dd MMM yyyy}";
    public string ComparisonLabel => $"{Month:dd MMM} - {CurrentComparisonEnd:dd MMM yyyy} versus {ComparisonStart:dd MMM} - {ComparisonEnd:dd MMM yyyy}";
    public bool IsCurrentMonth { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal Income { get; set; }
    public int IncomeCount { get; set; }
    public int TransactionCount { get; set; }
    public int ActiveDays { get; set; }
    public int DaysInPeriod => (PeriodEnd-Month).Days+1;
    public decimal AverageTransaction => TransactionCount > 0 ? TotalExpenses/TransactionCount : 0m;
    public decimal AveragePerDay => DaysInPeriod > 0 ? TotalExpenses/DaysInPeriod : 0m;
    public bool HasPatternData => TransactionCount >= 5 && ActiveDays >= 3;
    public decimal ComparableCurrentExpenses { get; set; }
    public int ComparableCurrentCount { get; set; }
    public decimal PreviousExpenses { get; set; }
    public int PreviousCount { get; set; }
    public decimal Change => ComparableCurrentExpenses-PreviousExpenses;
    public decimal? ChangePercent => PreviousExpenses > 0 ? Change/PreviousExpenses*100m : null;
    public List<SpendingCategoryInsight> Categories { get; set; } = new();
    public List<SpendingDayInsight> Weekdays { get; set; } = new();
    public List<BudgetSummaryViewModel> Budgets { get; set; } = new();
    public List<SpendingRecommendation> Recommendations { get; set; } = new();
    public List<SpendingCategoryInsight> HighestCategories => Categories.Where(c=>c.Amount>0m&&c.Amount==Categories.Max(x=>x.Amount)).ToList();
    public List<SpendingCategoryInsight> MostFrequentCategories => Categories.Where(c=>c.Count>0&&c.Count==Categories.Max(x=>x.Count)).ToList();
    public int OverspentBudgets => Budgets.Count(b=>b.AmountSpent>b.BudgetLimit);
    public decimal OverBudgetAmount => Budgets.Sum(b=>Math.Max(0m,b.AmountSpent-b.BudgetLimit));
    public decimal UnbudgetedSpending => Categories.Where(c=>!c.HasBudget).Sum(c=>c.Amount);
}
public sealed class SpendingCategoryInsight
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PreviousAmount { get; set; }
    public decimal ComparableAmount { get; set; }
    public decimal Change => ComparableAmount-PreviousAmount;
    public int Count { get; set; }
    public int ActiveDays { get; set; }
    public decimal Share { get; set; }
    public bool HasBudget { get; set; }
    public decimal Average => Count>0?Amount/Count:0m;
}
public sealed class SpendingDayInsight
{
    public string Day { get; set; } = "";
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
public sealed class SpendingRecommendation
{
    public string Rule { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public string Action { get; set; } = "Budget";
}
