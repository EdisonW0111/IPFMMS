using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Original Figure 4.24 attribute names are preserved.
public class SharedSavingsGoal
{
    [Key,StringLength(20)] public string sharedSavingsGoalId { get; set; } = "";
    [Required,StringLength(20)] public string sharedFinanceId { get; set; } = "";
    [Column(TypeName="decimal(12,2)")] public decimal targetAmount { get; set; }
    [Required,StringLength(20)] public string status { get; set; } = "Active";
    [ForeignKey(nameof(sharedFinanceId))] public SharedFinance? Finance { get; set; }
}
