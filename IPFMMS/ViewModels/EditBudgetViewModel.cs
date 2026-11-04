using System.ComponentModel.DataAnnotations;

namespace IPFMMS.ViewModels
{
    public class EditBudgetViewModel : IValidatableObject
    {
        [Required]
        public string BudgetId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime BudgetMonth { get; set; }

        [Required]
        [RegularExpression("^(FixedAmount|Percentage)$", ErrorMessage = "Please select a valid budget type.")]
        [Display(Name = "Budget Type")]
        public string BudgetType { get; set; } = "FixedAmount";

        [Display(Name = "Budget Amount (RM)")]
        public decimal? BudgetAmount { get; set; }

        [Display(Name = "Budget Percentage (%)")]
        public decimal? BudgetPercentage { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        [Display(Name = "Notes (Optional)")]
        public string? Notes { get; set; }

        [Display(Name = "Repeat monthly")]
        public bool RepeatMonthly { get; set; }

        [Display(Name = "Repeat until (optional)")]
        public string? RepeatUntil { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (BudgetType == "FixedAmount" && (!BudgetAmount.HasValue || BudgetAmount < 0.01m || BudgetAmount > 9999999999.99m))
                yield return new ValidationResult("Budget amount must be between RM0.01 and RM9,999,999,999.99.", new[] { nameof(BudgetAmount) });
            if (BudgetType == "FixedAmount" && BudgetAmount is decimal amount && decimal.Round(amount, 2) != amount)
                yield return new ValidationResult("Budget amount can have at most two decimal places.", new[] { nameof(BudgetAmount) });
            if (BudgetType == "Percentage" && (!BudgetPercentage.HasValue || BudgetPercentage < 1m || BudgetPercentage > 100m))
                yield return new ValidationResult("Percentage must be between 1% and 100%.", new[] { nameof(BudgetPercentage) });
            if (BudgetType == "Percentage" && BudgetPercentage is decimal percentage && decimal.Round(percentage, 2) != percentage)
                yield return new ValidationResult("Percentage can have at most two decimal places.", new[] { nameof(BudgetPercentage) });
        }
    }

    public class DeleteBudgetViewModel
    {
        public bool CanDeleteSeries { get; set; }
        public int FutureSeriesCount { get; set; }
        public bool HasUnlinkedCategoryBudgets { get; set; }
        public string BudgetId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime BudgetMonth { get; set; }
        public string BudgetType { get; set; } = string.Empty;
        public decimal? BudgetAmount { get; set; }
        public decimal? BudgetPercentage { get; set; }
    }
}
