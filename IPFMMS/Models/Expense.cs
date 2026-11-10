using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IPFMMS.Models
{
    public class Expense
    {
        [Key]
        [StringLength(20)]
        public string ExpenseId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Expense category is required.")]
        [StringLength(20)]
        public string ExpenseCategoryId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(typeof(decimal), "0.01", "999999999.99",
            ErrorMessage = "Amount must be greater than RM0.")]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Expense date is required.")]
        [DataType(DataType.Date)]
        public DateTime ExpenseDate { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        [StringLength(30)]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        // Optional uploaded receipt file path
        [StringLength(500)]
        public string? ReceiptPath { get; set; }

        public DateTime CreatedDate { get; set; }
            = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;

        // Chapter 4 attribute names share the existing persisted values. These aliases
        // add no database columns and keep existing serialization contracts unchanged.
        [NotMapped, JsonIgnore]
        public string expenseId { get => ExpenseId; set => ExpenseId = value; }
        [NotMapped, JsonIgnore]
        public string userId { get => UserId; set => UserId = value; }
        [NotMapped, JsonIgnore]
        public string expenseCategoryId { get => ExpenseCategoryId; set => ExpenseCategoryId = value; }
        [NotMapped, JsonIgnore]
        public decimal amount { get => Amount; set => Amount = value; }
        [NotMapped, JsonIgnore]
        public DateTime expenseDate { get => ExpenseDate; set => ExpenseDate = value; }
        [NotMapped, JsonIgnore]
        public string paymentMethod { get => PaymentMethod; set => PaymentMethod = value; }
        [NotMapped, JsonIgnore]
        public string? description { get => Description; set => Description = value; }
        [NotMapped, JsonIgnore]
        public string? receiptPath { get => ReceiptPath; set => ReceiptPath = value; }
        [NotMapped, JsonIgnore]
        public DateTime createdDate { get => CreatedDate; set => CreatedDate = value; }
    }
}
