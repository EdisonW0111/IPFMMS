namespace IPFMMS.ViewModels
{
    public class SavingsIndexViewModel
    {
        public List<SavingsGoalSummaryViewModel> Goals { get; set; } = new();
        public decimal TotalTarget => Goals.Sum(g => g.TargetAmount);
        public decimal TotalSaved => Goals.Sum(g => g.TotalSaved);
        // Surplus savings in one goal do not reduce another goal's shortfall.
        public decimal TotalRemaining => Goals.Sum(g => g.Remaining);
        public int CompletedGoals => Goals.Count(g => g.IsCompleted);
        public int ActiveGoals => Goals.Count(g => g.Status == "Active" && !g.IsCompleted);
    }

    public class SavingsGoalSummaryViewModel
    {
        public string SavingsGoalId { get; set; } = string.Empty;
        public string GoalName { get; set; } = string.Empty;
        public string GoalType { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public DateTime TargetDate { get; set; }
        public string SavingsMethod { get; set; } = string.Empty;
        public int? AutomaticSavingDay { get; set; }
        public DateTime? NextAutomaticSavingDate { get; set; }
        public decimal? MonthlyTargetAmount { get; set; }
        public string Status { get; set; } = "Active";
        public decimal TotalSaved { get; set; }
        public int ContributionCount { get; set; }
        public DateTime AsOfDate { get; set; }

        public decimal? AutomaticSavingsBaseline { get; set; }
        public decimal AutomaticPaid { get; set; }
        public decimal AutomaticCreditUsed { get; set; }
        public decimal ExtraCredit => Math.Max(0m, TotalSaved - (AutomaticSavingsBaseline ?? TotalSaved) - AutomaticPaid - AutomaticCreditUsed);
        public decimal NextDeduction => Math.Min(Remaining, Math.Max(0m, (MonthlyTargetAmount ?? 0m) - ExtraCredit));
        public decimal Remaining => Math.Max(0m, TargetAmount - TotalSaved);
        public decimal ProgressPercent => TargetAmount > 0m
            ? Math.Clamp(TotalSaved / TargetAmount * 100m, 0m, 100m) : 0m;
        public bool IsCompleted => Status == "Completed" || (TargetAmount > 0m && TotalSaved >= TargetAmount);
        public string DisplayStatus => IsCompleted ? "Completed"
            : Status == "Active" && TargetDate.Date < AsOfDate.Date ? "Overdue" : Status;
    }
}
