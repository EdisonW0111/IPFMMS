using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Original Figure 4.24 attribute names are preserved.
public class SharedFinance
{
    [Key,StringLength(20)] public string sharedFinanceId { get; set; } = "";
    [Required,StringLength(100)] public string sharedFinanceName { get; set; } = "";
    [Required,StringLength(30)] public string financeType { get; set; } = "";
    [StringLength(500)] public string? description { get; set; }
    public DateTime createdDate { get; set; } = DateTime.UtcNow;
    [Required,StringLength(20)] public string status { get; set; } = "Active";
    [Required,StringLength(20)] public string ownerId { get; set; } = "";
    public int revision { get; set; } = 1;
    [ForeignKey(nameof(ownerId))] public User? Owner { get; set; }
}
