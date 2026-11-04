using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IPFMMS.Models
{
    public class Budget
    {
        [Key]
        [StringLength(20)]
        public string BudgetId { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ExpenseCategoryId { get; set; }
            = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime BudgetMonth { get; set; }

        // FixedAmount or Percentage
        [Required]
        [StringLength(20)]
        public string BudgetType { get; set; }
            = "FixedAmount";

        [Column(TypeName = "decimal(12,2)")]
        public decimal? BudgetAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? BudgetPercentage { get; set; }

        public DateTime CreatedDate { get; set; }
            = DateTime.UtcNow;

        [StringLength(500)]
        public string? Notes { get; set; }

        // Null on independent and pre-linkage budgets. Never infer series from category alone.
        [StringLength(20)]
        public string? BudgetScheduleId { get; set; }
        public int? RepeatSeriesVersion { get; set; }
        public BudgetSchedule? BudgetSchedule { get; set; }

        // Navigation properties
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(ExpenseCategoryId))]
        public ExpenseCategory? ExpenseCategory { get; set; }
    }
}