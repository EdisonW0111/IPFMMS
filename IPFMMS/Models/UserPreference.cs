using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IPFMMS.Models;

// Additional per-account preferences; original Chapter 4 entities remain intact.
public sealed class UserPreference
{
    [Key, StringLength(20)] public string UserId { get; set; } = "";
    [Required, StringLength(10)] public string Theme { get; set; } = "Light";
    public bool BillInApp { get; set; } = true;
    public bool BillEmail { get; set; } = true;
    public bool MonitoringInApp { get; set; } = true;
    public bool MonitoringEmail { get; set; } = true;
    public bool InvitationEmail { get; set; } = true;
    public int Revision { get; set; } = 1;
    [ForeignKey(nameof(UserId))] public User? User { get; set; }
}
