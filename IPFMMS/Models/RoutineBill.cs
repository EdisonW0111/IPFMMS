using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Original Figure 4.24 attributes preserve their exact names.
public class RoutineBill
{
    [Key, StringLength(20)] public string billId { get; set; } = "";
    [Required, StringLength(20)] public string userId { get; set; } = "";
    [Required, StringLength(20)] public string expenseCategoryId { get; set; } = "";
    [Required, StringLength(100)] public string billName { get; set; } = "";
    [Required, StringLength(30)] public string billType { get; set; } = "Bill";
    [Column(TypeName="decimal(12,2)")] public decimal amount { get; set; }
    [Column(TypeName="date")] public DateTime dueDate { get; set; }
    [Required, StringLength(20)] public string frequency { get; set; } = "Monthly";
    [Required, StringLength(30)] public string paymentMethod { get; set; } = "";
    public int reminderPeriod { get; set; } = 3;
    [StringLength(500)] public string? description { get; set; }
    [Required, StringLength(20)] public string status { get; set; } = "Active";
    public DateTime createdDate { get; set; } = DateTime.UtcNow;
    // Extra: retain the chosen day across short months (31 Jan -> 28 Feb -> 31 Mar).
    public int recurrenceAnchorDay { get; set; } = 1;
    [ForeignKey(nameof(userId))] public User? User { get; set; }
    [ForeignKey(nameof(expenseCategoryId))] public ExpenseCategory? ExpenseCategory { get; set; }
}
