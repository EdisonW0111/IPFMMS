using System.ComponentModel.DataAnnotations;
using IPFMMS.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IPFMMS.ViewModels
{
    public class EditSavingsGoalViewModel : IValidatableObject
    {
        [Required]
        [StringLength(20)]
        public string SavingsGoalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a goal name.")]
        [StringLength(100, ErrorMessage = "Goal name cannot exceed 100 characters.")]
        [Display(Name = "Goal Name")]
        public string GoalName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a goal type.")]
        [RegularExpression("^(ShortTerm|LongTerm)$", ErrorMessage = "Please select a valid goal type.")]
        [Display(Name = "Goal Type")]
        public string GoalType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a target amount.")]
        [Range(typeof(decimal), "0.01", "9999999999.99",
            ErrorMessage = "Target amount must be between RM0.01 and RM9,999,999,999.99.")]
        [Display(Name = "Target Amount (RM)")]
        public decimal? TargetAmount { get; set; }

        [Required(ErrorMessage = "Please select a target date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Target Date")]
        public DateTime? TargetDate { get; set; }

        [Required(ErrorMessage = "Please select a savings method.")]
        [RegularExpression("^(Manual|AutomaticMonthlyTarget)$", ErrorMessage = "Please select a valid savings method.")]
        [Display(Name = "Savings Method")]
        public string SavingsMethod { get; set; } = string.Empty;

        [Range(1, 28), Display(Name = "Monthly saving day (1-28)")]
        public int? AutomaticSavingDay { get; set; }

        // The controller must reload this contribution total from the owned
        // goal before validation and again inside the update transaction.
        [BindNever]
        [Display(Name = "Already Saved (RM)")]
        public decimal TotalSaved { get; set; }

        [BindNever] public decimal? ExistingMonthlyTarget { get; set; }
        [BindNever] public decimal OriginalTarget { get; set; }
        [BindNever] public DateTime OriginalDate { get; set; }
        [BindNever] public int? OriginalDay { get; set; }
        public decimal? RemainingAmount => TargetAmount.HasValue
            ? Math.Max(0m, TargetAmount.Value - TotalSaved) : null;

        // Calculated only: a posted value cannot replace this getter.
        [Display(Name = "Monthly Target Amount (RM)")]
        public decimal? MonthlyTargetAmount
        {
            get
            {
                if (SavingsMethod != "AutomaticMonthlyTarget") return null;
                if (ExistingMonthlyTarget > 0m && TargetAmount == OriginalTarget && TargetDate?.Date == OriginalDate.Date && AutomaticSavingDay == OriginalDay)
                    return ExistingMonthlyTarget;
                if (RemainingAmount == 0m) return 0m;
                var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
                return AutomaticSavingsPlan.Monthly(RemainingAmount, TargetDate, today, AutomaticSavingDay);
            }
        }

        public List<SelectListItem> GoalTypes { get; set; } = new()
        {
            new SelectListItem { Value = "ShortTerm", Text = "Short-term" },
            new SelectListItem { Value = "LongTerm", Text = "Long-term" }
        };

        public List<SelectListItem> SavingsMethods { get; set; } = new()
        {
            new SelectListItem { Value = "Manual", Text = "Manual contributions" },
            new SelectListItem { Value = "AutomaticMonthlyTarget", Text = "Automatic monthly saving" }
        };

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
            if (SavingsMethod == "AutomaticMonthlyTarget" &&
                IPFMMS.Services.AutomaticSavingsPlan.Monthly(RemainingAmount, TargetDate, today, AutomaticSavingDay) == null)
                yield return new ValidationResult("Choose a day from 1-28 with at least one deduction on or before the target date.", new[] { nameof(AutomaticSavingDay) });
            if (TargetDate.HasValue && (TargetDate.Value.Date < today || TargetDate.Value.Year > 9998))
                yield return new ValidationResult("Target date must be today or a future date, up to 31 December 9998.",
                    new[] { nameof(TargetDate) });

            if (TargetAmount is decimal amount)
            {
                if (decimal.Round(amount, 2) != amount)
                    yield return new ValidationResult("Target amount can have at most two decimal places.",
                        new[] { nameof(TargetAmount) });
                if (amount < TotalSaved)
                    yield return new ValidationResult("Target amount cannot be less than the amount already contributed.",
                        new[] { nameof(TargetAmount) });
            }
        }
    }
}
