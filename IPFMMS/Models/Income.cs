using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IPFMMS.Models
{
    public class Income
    {
        [Key]
        [StringLength(20)]
        public string IncomeId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Income category is required.")]
        [StringLength(20)]
        public string IncomeCategoryId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(typeof(decimal), "0.01", "999999999.99",
            ErrorMessage = "Amount must be greater than RM0.")]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Income date is required.")]
        [DataType(DataType.Date)]
        public DateTime IncomeDate { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        public DateTime CreatedDate { get; set; }
            = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;
    }
}
