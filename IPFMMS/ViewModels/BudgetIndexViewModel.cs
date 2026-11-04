using System;
using System.Collections.Generic;
using System.Linq;

namespace IPFMMS.ViewModels
{
    // Display calculations only; these properties are not database columns.
    public class BudgetIndexViewModel
    {
        public List<string> RolloverWarnings { get; set; } = new();
        public DateTime RolloverLimitMonth { get; set; }
        public DateTime BudgetMonth { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public List<BudgetSummaryViewModel> Budgets { get; set; } = new();
        public decimal TotalBudget => Budgets.Sum(b => b.BudgetLimit);
        public decimal BudgetedSpending => Budgets.Sum(b => b.AmountSpent);
        public decimal RemainingBudget => TotalBudget - BudgetedSpending;
        public decimal UnbudgetedSpending => MonthlyExpenses - BudgetedSpending;
    }

    public class BudgetSummaryViewModel
    {
        public string BudgetId { get; set; } = string.Empty;
        // The current category schedule, including on historical monthly cards.
        public bool IsRepeating { get; set; }
        public DateTime? RepeatUntil { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string BudgetType { get; set; } = string.Empty;
        public decimal? BudgetPercentage { get; set; }
        public decimal? BudgetAmount { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal AmountSpent { get; set; }
        public string? Notes { get; set; }

        public decimal BudgetLimit => BudgetType == "Percentage"
            ? decimal.Round(MonthlyIncome * (BudgetPercentage ?? 0m) / 100m,
                2, MidpointRounding.AwayFromZero)
            : BudgetAmount ?? 0m;

        public decimal Remaining => BudgetLimit - AmountSpent;
        public decimal? Utilization => BudgetLimit > 0m
            ? AmountSpent / BudgetLimit * 100m : null;
        public decimal Progress => Math.Clamp(Utilization ?? (AmountSpent > 0m ? 100m : 0m), 0m, 100m);

        public string Status => AmountSpent > BudgetLimit ? "Overspent"
            : BudgetLimit <= 0m ? "No budget available"
            : AmountSpent == BudgetLimit ? "Limit reached"
            : Utilization >= 80m ? "Warning" : "Healthy";
    }
}
