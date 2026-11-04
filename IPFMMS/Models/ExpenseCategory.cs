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
    }
}