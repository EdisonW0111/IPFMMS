using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Added history entity; each scheduled occurrence links to one recorded expense.
public class RoutineBillPayment
{
    [Key, StringLength(20)] public string paymentId { get; set; } = "";
    [Required, StringLength(20)] public string billId { get; set; } = "";
    [Required, StringLength(20)] public string expenseId { get; set; } = "";
    [Column(TypeName="date")] public DateTime dueDate { get; set; }
    [Column(TypeName="date")] public DateTime paidDate { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal amount { get; set; }
    [Required, StringLength(30)] public string paymentMethod { get; set; } = "";
    [Required, StringLength(100)] public string billNameSnapshot { get; set; } = "";
    public DateTime createdDate { get; set; } = DateTime.UtcNow;
    [ForeignKey(nameof(billId))] public RoutineBill? Bill { get; set; }
    [ForeignKey(nameof(expenseId))] public Expense? Expense { get; set; }
}
