using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Original Figure 4.24 attribute names are preserved.
public class SharedBudget
{
    [Key,StringLength(20)] public string sharedBudgetId { get; set; } = "";
    [Required,StringLength(20)] public string sharedFinanceId { get; set; } = "";
    [Column(TypeName="decimal(12,2)")] public decimal budgetAmount { get; set; }
    [Column(TypeName="date")] public DateTime budgetMonth { get; set; }
    [ForeignKey(nameof(sharedFinanceId))] public SharedFinance? Finance { get; set; }
}
