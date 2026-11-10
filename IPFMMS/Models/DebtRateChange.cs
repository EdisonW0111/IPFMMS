using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
public class DebtRateChange
{
    [Key,StringLength(20)] public string rateChangeId { get; set; } = "";
    [Required,StringLength(20)] public string debtId { get; set; } = "";
    [Column(TypeName="date")] public DateTime effectiveDueDate { get; set; }
    [Column(TypeName="decimal(7,4)")] public decimal previousRate { get; set; }
    [Column(TypeName="decimal(7,4)")] public decimal newRate { get; set; }
    [Column(TypeName="decimal(7,4)")] public decimal referenceRate { get; set; }
    [Column(TypeName="decimal(7,4)")] public decimal spread { get; set; }
    [Column(TypeName="decimal(12,2)")] public decimal monthlyPayment { get; set; }
    public DateTime createdDate { get; set; } = DateTime.UtcNow;
    [ForeignKey(nameof(debtId))] public Debt? Debt { get; set; }
}
