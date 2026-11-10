using System.Text.Json.Serialization;
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

        // Chapter 4 attribute names share the existing persisted values. These aliases
        // add no database columns and keep existing serialization contracts unchanged.
        [NotMapped, JsonIgnore]
        public string budgetId { get => BudgetId; set => BudgetId = value; }
        [NotMapped, JsonIgnore]
        public string userId { get => UserId; set => UserId = value; }
        [NotMapped, JsonIgnore]
        public string expenseCategoryId { get => ExpenseCategoryId; set => ExpenseCategoryId = value; }
        [NotMapped, JsonIgnore]
        public DateTime budgetMonth { get => BudgetMonth; set => BudgetMonth = value; }
        [NotMapped, JsonIgnore]
        public string budgetType { get => BudgetType; set => BudgetType = value; }
        [NotMapped, JsonIgnore]
        public decimal? budgetAmount { get => BudgetAmount; set => BudgetAmount = value; }
        [NotMapped, JsonIgnore]
        public decimal? budgetPercentage { get => BudgetPercentage; set => BudgetPercentage = value; }
        [NotMapped, JsonIgnore]
        public DateTime createdDate { get => CreatedDate; set => CreatedDate = value; }
        [NotMapped, JsonIgnore]
        public string? notes { get => Notes; set => Notes = value; }
    }
}