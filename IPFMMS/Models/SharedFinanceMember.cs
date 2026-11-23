using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Original Figure 4.24 attribute names are preserved.
public class SharedFinanceMember
{
    [StringLength(20)] public string sharedFinanceId { get; set; } = "";
    [StringLength(20)] public string userId { get; set; } = "";
    [Required,StringLength(20)] public string memberRole { get; set; } = "Member";
    [Required,StringLength(20)] public string invitationStatus { get; set; } = "Pending";
    public DateTime invitedDate { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "decimal(5,2)")] public decimal? sharePercentage { get; set; }
    public DateTime? joinedDate { get; set; }
    public int invitationVersion { get; set; } = 1;
    [StringLength(20)] public string emailStatus { get; set; } = "Pending";
    public DateTime? emailAttemptedDate { get; set; }
    public DateTime? emailSentDate { get; set; }
    [ForeignKey(nameof(sharedFinanceId))] public SharedFinance? Finance { get; set; }
    [ForeignKey(nameof(userId))] public User? User { get; set; }
}
