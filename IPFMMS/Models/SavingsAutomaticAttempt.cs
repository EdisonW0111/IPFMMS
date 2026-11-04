using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
public class SavingsAutomaticAttempt
{
    public long Id { get; set; }
    [Required, StringLength(20)] public string SavingsGoalId { get; set; } = "";
    [Column(TypeName="date")] public DateTime DueDate { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal PlannedAmount { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal CreditApplied { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal Amount { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
    public SavingsGoal? SavingsGoal { get; set; }
}
