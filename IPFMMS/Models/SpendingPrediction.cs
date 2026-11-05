using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;

// Original Figure 4.24 attributes retain their exact names.
public class SpendingPrediction
{
    [Key, StringLength(20)] public string predictionId { get; set; } = "";
    [Required, StringLength(20)] public string userId { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal predictedAmount { get; set; }
    [Required, StringLength(30)] public string trend { get; set; } = "";
    [Column(TypeName = "date")] public DateTime predictionMonth { get; set; }
    public DateTime calculatedDate { get; set; }
    [ForeignKey(nameof(userId))] public User? User { get; set; }
}
