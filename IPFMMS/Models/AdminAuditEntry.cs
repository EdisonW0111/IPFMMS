using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;

// Supporting management history; the original Figure 4.24 entities stay unchanged.
public sealed class AdminAuditEntry
{
    [Key, StringLength(20)] public string AuditId { get; set; } = "";
    [Required, StringLength(20)] public string AdminUserId { get; set; } = "";
    [Required, StringLength(30)] public string EntityType { get; set; } = "";
    [Required, StringLength(20)] public string EntityId { get; set; } = "";
    [Required, StringLength(40)] public string Action { get; set; } = "";
    [Required, StringLength(255)] public string Summary { get; set; } = "";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [ForeignKey(nameof(AdminUserId))] public User? Admin { get; set; }
}
