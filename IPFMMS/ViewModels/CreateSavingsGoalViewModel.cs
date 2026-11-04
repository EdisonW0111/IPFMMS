using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IPFMMS.ViewModels
{
    // Form input only. Owner, ID, status and creation date are set by the server.
    public class CreateSavingsGoalViewModel : IValidatableObject
    {
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

        // Calculated by the server for AutomaticMonthlyTarget. Manual goals
        // persist null; users do not enter this value.
        [Display(Name = "Monthly Target Amount (RM)")]
        public decimal? MonthlyTargetAmount { get; set; }

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
        // Malaysia's current date, independent of the Azure host timezone.
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
            if (SavingsMethod == "AutomaticMonthlyTarget" &&
                IPFMMS.Services.AutomaticSavingsPlan.Monthly(TargetAmount, TargetDate, today, AutomaticSavingDay) == null)
                yield return new ValidationResult("Choose a day from 1-28 with at least one deduction on or before the target date.", new[] { nameof(AutomaticSavingDay) });
            if (TargetDate.HasValue && (TargetDate.Value.Date < today || TargetDate.Value.Year > 9998))
                yield return new ValidationResult("Target date must be today or a future date, up to 31 December 9998.",
                    new[] { nameof(TargetDate) });

            if (TargetAmount is decimal target && decimal.Round(target, 2) != target)
                yield return new ValidationResult("Target amount can have at most two decimal places.",
                    new[] { nameof(TargetAmount) });

            if (SavingsMethod == "AutomaticMonthlyTarget")
            {
                if (!MonthlyTargetAmount.HasValue || MonthlyTargetAmount < 0.01m || MonthlyTargetAmount > 9999999999.99m)
                    yield return new ValidationResult("A valid target amount and date are required to calculate the monthly target.",
                        new[] { nameof(MonthlyTargetAmount) });
                if (MonthlyTargetAmount is decimal monthly && decimal.Round(monthly, 2) != monthly)
                    yield return new ValidationResult("Monthly target can have at most two decimal places.",
                        new[] { nameof(MonthlyTargetAmount) });
            }
        }
    }
}
