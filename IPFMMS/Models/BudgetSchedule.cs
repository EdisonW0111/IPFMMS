using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IPFMMS.Models
{
    // Repeating settings are separate from Budget's monthly history.
    // One schedule is retained per owner/category, including when disabled.
    public class BudgetSchedule
    {
        [Key]
        [StringLength(20)]
        public string BudgetScheduleId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ExpenseCategoryId { get; set; } = string.Empty;

        // First day of the next month eligible for creation. Future rollover
        // logic must advance this even when that month's budget already exists.
        [DataType(DataType.Date)]
        public DateTime NextBudgetMonth { get; set; }

        // Optional inclusive final month. Null means repeat until turned off.
        // Store the first day of the selected month, as with other month fields.
        [DataType(DataType.Date)]
        public DateTime? EndMonth { get; set; }

        // Explicit opt-in. Disabling a schedule preserves its settings and
        // prevents rollover from falling back to older Budget records.
        public bool IsActive { get; set; }
        public int SeriesVersion { get; set; } = 1;

        [Required]
        [StringLength(20)]
        public string BudgetType { get; set; } = "FixedAmount";

        [Column(TypeName = "decimal(12,2)")]
        public decimal? BudgetAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? BudgetPercentage { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(ExpenseCategoryId))]
        public ExpenseCategory? ExpenseCategory { get; set; }
    }
}
