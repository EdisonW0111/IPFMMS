using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace IPFMMS.Models
{
    public class ExpenseCategory
    {
        [Key]
        [StringLength(20)]
        public string ExpenseCategoryId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; } = 1000;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // One expense category can be used by many expense records.
        public ICollection<Expense> Expenses { get; set; }
            = new List<Expense>();

        // Chapter 4 attribute names share the existing persisted values. These aliases
        // add no database columns and keep existing serialization contracts unchanged.
        [NotMapped, JsonIgnore]
        public string expenseCategoryId { get => ExpenseCategoryId; set => ExpenseCategoryId = value; }
        [NotMapped, JsonIgnore]
        public string categoryName { get => CategoryName; set => CategoryName = value; }
        [NotMapped, JsonIgnore]
        public string? description { get => Description; set => Description = value; }
        [NotMapped, JsonIgnore]
        public string status
        {
            get => IsActive ? "Active" : "Inactive";
            set => IsActive = value switch
            {
                "Active" => true,
                "Inactive" => false,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Status must be Active or Inactive.")
            };
        }
        [NotMapped, JsonIgnore]
        public int displayOrder { get => DisplayOrder; set => DisplayOrder = value; }
    }
}