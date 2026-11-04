using System.ComponentModel.DataAnnotations;

namespace IPFMMS.Models
{
    public class IncomeCategory
    {
        [Key]
        [StringLength(20)]
        public string IncomeCategoryId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; } = 1000;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // One income category can be used by many income records.
        public ICollection<Income> Incomes { get; set; }
            = new List<Income>();
    }
}