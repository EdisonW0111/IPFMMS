using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
public class DebtRepayment
{
    [Key,StringLength(20)] public string repaymentId { get; set; } = "";
    [Required,StringLength(20)] public string debtId { get; set; } = "";
    [StringLength(20)] public string? expenseId { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal repaymentAmount { get; set; }
    [Column(TypeName="date")] public DateTime repaymentDate { get; set; }
    [Required,StringLength(30)] public string paymentMethod { get; set; } = "";
    [StringLength(500)] public string? remark { get; set; }
    public DateTime createdDate { get; set; } = DateTime.UtcNow;
    [Column(TypeName="decimal(12,2)")] public decimal principalAmount { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal interestAmount { get; set; }
    [Column(TypeName="decimal(7,4)")] public decimal appliedInterestRate { get; set; }
    [Column(TypeName="date")] public DateTime? scheduledDueDate { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal periodInterest { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal scheduledPayment { get; set; }
    [Required,StringLength(20)] public string allocationMethod { get; set; } = "Manual";
    [ForeignKey(nameof(debtId))] public Debt? Debt { get; set; }
    [ForeignKey(nameof(expenseId))] public Expense? Expense { get; set; }
}
