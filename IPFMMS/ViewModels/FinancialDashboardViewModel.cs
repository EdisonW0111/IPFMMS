namespace IPFMMS.ViewModels
{
    public class FinancialDashboardViewModel
    {
        public int UpcomingBillCount { get; set; }
        public int OverdueBillCount { get; set; }
        public List<DashboardBillViewModel> UpcomingBills { get; set; } = new();
        public List<DashboardBillViewModel> OverdueBills { get; set; } = new();
        public List<DashboardActivityViewModel> RecentActivities { get; set; } = new();
        public FinancialHealthViewModel? FinancialHealth { get; set; }
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
    public sealed class DashboardBillViewModel
    {
        public string BillId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }
    public sealed class DashboardActivityViewModel
    {
        public string Id { get; set; } = "";
        public DateTime Date { get; set; }
        public DateTime RecordedAt { get; set; }
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string Controller { get; set; } = "";
        public string Action { get; set; } = "";
        public string? RouteId { get; set; }
        public string? ExpenseId { get; set; }
        public string Label => Type switch { "Income"=>"Income", "Expense"=>"Expense", "Savings"=>"Savings allocation", "SharedSavings"=>"Shared savings allocation", "SharedRelease"=>"Allocation released", _=>Type };
        public string Prefix => Type is "Income" or "SharedRelease" ? "+" : "−";
    }
}
