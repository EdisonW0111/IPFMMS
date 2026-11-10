using System.Text.Json.Serialization;
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

        // Nullable so existing transactions retain unknown historical details.
        [StringLength(30)]
        public string? PaymentMethod { get; set; }

        [StringLength(100)]
        public string? Reference { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        public DateTime CreatedDate { get; set; }
            = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;

        // Chapter 4 attribute names share the existing persisted values. These aliases
        // add no database columns and keep existing serialization contracts unchanged.
        [NotMapped, JsonIgnore]
        public string incomeId { get => IncomeId; set => IncomeId = value; }
        [NotMapped, JsonIgnore]
        public string userId { get => UserId; set => UserId = value; }
        [NotMapped, JsonIgnore]
        public string incomeCategoryId { get => IncomeCategoryId; set => IncomeCategoryId = value; }
        [NotMapped, JsonIgnore]
        public decimal amount { get => Amount; set => Amount = value; }
        [NotMapped, JsonIgnore]
        public DateTime incomeDate { get => IncomeDate; set => IncomeDate = value; }
        [NotMapped, JsonIgnore]
        public string? description { get => Description; set => Description = value; }
        [NotMapped, JsonIgnore]
        public string? paymentMethod { get => PaymentMethod; set => PaymentMethod = value; }
        [NotMapped, JsonIgnore]
        public string? reference { get => Reference; set => Reference = value; }
        [NotMapped, JsonIgnore]
        public DateTime createdDate { get => CreatedDate; set => CreatedDate = value; }
    }
}
