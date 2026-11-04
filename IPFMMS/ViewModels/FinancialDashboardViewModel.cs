namespace IPFMMS.ViewModels
{
    public class FinancialDashboardViewModel
    {
        public DateTime AsOfDate { get; set; }
        public DateTime CurrentMonth => new(AsOfDate.Year, AsOfDate.Month, 1);

        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalSavingsAllocated { get; set; }
        public decimal OverallNetBalance => TotalIncome - TotalExpenses;
        public decimal AvailableToSpend => OverallNetBalance - TotalSavingsAllocated;

        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal MonthlySavingsAllocated { get; set; }
        public decimal MonthlyNet => MonthlyIncome - MonthlyExpenses;
        public decimal MonthlyRemaining => MonthlyNet - MonthlySavingsAllocated;

        public BudgetIndexViewModel BudgetSummary { get; set; } = new();
        public SavingsIndexViewModel SavingsSummary { get; set; } = new();
        public int HealthyBudgetCount => BudgetSummary.Budgets.Count(b => b.Status == "Healthy");
        public int WarningBudgetCount => BudgetSummary.Budgets.Count(b => b.Status == "Warning");
        public int ReachedBudgetCount => BudgetSummary.Budgets.Count(b => b.Status is "Overspent" or "Limit reached");
        public int OverdueSavingsGoalCount => SavingsSummary.Goals.Count(g => g.DisplayStatus == "Overdue");
    }
}
