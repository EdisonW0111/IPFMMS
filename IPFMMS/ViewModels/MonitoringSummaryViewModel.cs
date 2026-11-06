namespace IPFMMS.ViewModels;
public sealed class MonitoringSummaryViewModel
{
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public DateTime Month { get; set; }
    public DateTime AsOfDate { get; set; }
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal SavingsContributions { get; set; }
    public decimal MonthlyRemaining => Income - Expenses - SavingsContributions;
    public List<MonitoringCategorySpending> Categories { get; set; } = new();
    public BudgetIndexViewModel Budgets { get; set; } = new();
    public int GoalCount { get; set; }
    public List<SavingsGoalSummaryViewModel> Goals { get; set; } = new();
    public FinancialHealthViewModel Health { get; set; } = new();
    public SpendingPredictionViewModel Prediction { get; set; } = new();
}
public sealed class MonitoringCategorySpending
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
}
