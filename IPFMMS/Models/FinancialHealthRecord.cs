using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;

// Attribute names follow Figure 4.24 exactly, including lower camel case.
public class FinancialHealthRecord
{
    [Key, StringLength(20)] public string healthRecordId { get; set; } = "";
    [Required, StringLength(20)] public string userId { get; set; } = "";
    [Column(TypeName="decimal(5,2)")] public decimal savingsRatioScore { get; set; }
    [Column(TypeName="decimal(5,2)")] public decimal budgetAdherenceScore { get; set; }
    [Column(TypeName="decimal(5,2)")] public decimal spendingStabilityScore { get; set; }
    [Column(TypeName="decimal(5,2)")] public decimal healthScore { get; set; }
    [Required, StringLength(30)] public string rating { get; set; } = "";
    [Column(TypeName="date")] public DateTime recordMonth { get; set; }
    public DateTime calculatedDate { get; set; }
    // Added to distinguish future calculation changes from the documented attributes.
    [Required, StringLength(20)] public string formulaVersion { get; set; } = "FHS-1";
    [ForeignKey(nameof(userId))] public User? User { get; set; }
}
