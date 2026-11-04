using System.Globalization;
using IPFMMS.ViewModels;

namespace IPFMMS.ViewModels;

public sealed class AnalyticsViewModel
{
    public DateTime FromMonth { get; set; }
    public DateTime ToMonth { get; set; }
    public string FromValue => FromMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public string ToValue => ToMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public string PeriodLabel => FromMonth == ToMonth ? FromMonth.ToString("MMMM yyyy")
        : $"{FromMonth:MMM yyyy} - {ToMonth:MMM yyyy}";
    public List<AnalyticsMonth> Months { get; set; } = new();
    public List<AnalyticsCategory> Categories { get; set; } = new();
    public List<AnalyticsBudget> Budgets { get; set; } = new();
    public decimal TotalIncome => Months.Sum(m => m.Income);
    public decimal TotalExpenses => Months.Sum(m => m.Expenses);
    public decimal TotalSavings => Months.Sum(m => m.Savings);
    public decimal NetCashFlow => TotalIncome - TotalExpenses;
    public decimal AfterSavings => NetCashFlow - TotalSavings;
    public decimal TotalBudgetLimit => Budgets.Sum(b => b.Summary.BudgetLimit);
    public decimal SpendingInBudgetedCategories => Budgets.Sum(b => b.Summary.AmountSpent);
    public decimal UnbudgetedSpending => TotalExpenses - SpendingInBudgetedCategories;
}

public sealed class AnalyticsMonth
{
    public DateTime Month { get; set; }
    public string Label => Month.ToString("MMM yyyy");
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Savings { get; set; }
    public decimal Net => Income - Expenses;
}

public sealed class AnalyticsCategory
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Share { get; set; }
}

public sealed class AnalyticsBudget
{
    public DateTime Month { get; set; }
    public BudgetSummaryViewModel Summary { get; set; } = new();
}
