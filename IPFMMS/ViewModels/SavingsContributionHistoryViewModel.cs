namespace IPFMMS.ViewModels
{
    public class SavingsContributionHistoryViewModel
    {
        public List<IPFMMS.Models.SavingsAutomaticAttempt> AutomaticAttempts { get; set; } = new();
        public const int PageSize = 10;
        public SavingsGoalSummaryViewModel Goal { get; set; } = new();
        public List<SavingsContributionRowViewModel> Contributions { get; set; } = new();
        public int PageNumber { get; set; } = 1;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Goal.ContributionCount / (decimal)PageSize));
        public int FirstRecord => Goal.ContributionCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
        public int LastRecord => (int)Math.Min((long)PageNumber * PageSize, Goal.ContributionCount);
    }

    public class SavingsContributionRowViewModel
    {
        public string ContributionId { get; set; } = string.Empty;
        public DateTime ContributionDate { get; set; }
        public decimal Amount { get; set; }
        public string ContributionMethod { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
