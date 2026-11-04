using System.ComponentModel.DataAnnotations;

namespace IPFMMS.ViewModels
{
    public class RecordSavingsContributionViewModel : IValidatableObject
    {
        [Required]
        [StringLength(20)]
        public string SavingsGoalId { get; set; } = string.Empty;

        // Display fields are always loaded from the owned goal, not the POST.
        public string? GoalName { get; set; }
        public string? GoalStatus { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal TotalSaved { get; set; }
        public decimal Remaining => Math.Max(0m, TargetAmount - TotalSaved);
        public bool CanContribute => GoalStatus == "Active" && Remaining > 0m;

        [Required(ErrorMessage = "Please enter a contribution amount.")]
        [Range(typeof(decimal), "0.01", "9999999999.99", ErrorMessage = "Please enter a valid amount greater than RM0.")]
        [Display(Name = "Contribution Amount (RM)")]
        public decimal? Amount { get; set; }

        [Required(ErrorMessage = "Please select a contribution date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Contribution Date")]
        public DateTime? ContributionDate { get; set; }
            = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;

        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
        [Display(Name = "Description (Optional)")]
        public string? Description { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Amount is decimal amount && decimal.Round(amount, 2) != amount)
                yield return new ValidationResult("Contribution amount can have at most two decimal places.", new[] { nameof(Amount) });
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
            if (ContributionDate.HasValue && (ContributionDate.Value.Year < 1000 || ContributionDate.Value.Date > today))
                yield return new ValidationResult("Contribution date must be today or a valid past date.", new[] { nameof(ContributionDate) });
        }
    }
}
