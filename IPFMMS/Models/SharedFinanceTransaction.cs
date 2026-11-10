using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Original Figure 4.24 attribute names are preserved.
public class SharedFinanceTransaction
{
    [Key,StringLength(20)] public string sharedTransactionId { get; set; } = "";
    [Required,StringLength(20)] public string sharedFinanceId { get; set; } = "";
    [Required,StringLength(20)] public string userId { get; set; } = "";
    [Required,StringLength(20)] public string expenseCategoryId { get; set; } = "";
    [Required,StringLength(30)] public string transactionType { get; set; } = "";
    [Column(TypeName="decimal(12,2)")] public decimal amount { get; set; }
    [Column(TypeName="date")] public DateTime transactionDate { get; set; }
    [StringLength(500)] public string? description { get; set; }
    [StringLength(20)] public string? expenseId { get; set; }
    [Required,StringLength(30)] public string paymentMethod { get; set; } = "Cash";
    public DateTime createdDate { get; set; } = DateTime.UtcNow;
    [Required,StringLength(32)] public string submissionKey { get; set; } = "";
    [ForeignKey(nameof(sharedFinanceId))] public SharedFinance? Finance { get; set; }
    [ForeignKey(nameof(userId))] public User? User { get; set; }
    [ForeignKey(nameof(expenseCategoryId))] public ExpenseCategory? ExpenseCategory { get; set; }
    [ForeignKey(nameof(expenseId))] public Expense? Expense { get; set; }
}
