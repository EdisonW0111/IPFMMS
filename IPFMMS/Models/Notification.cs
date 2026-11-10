using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;
// Original Figure 4.24 attributes retain their exact names.
public class Notification
{
    [Key, StringLength(20)] public string notificationId { get; set; } = "";
    [Required, StringLength(20)] public string userId { get; set; } = "";
    [StringLength(20)] public string? relatedUserId { get; set; }
    [Required, StringLength(40)] public string notificationType { get; set; } = "";
    [Required, StringLength(150)] public string title { get; set; } = "";
    [Required, StringLength(2000)] public string message { get; set; } = "";
    public DateTime createdDateTime { get; set; }
    public bool isRead { get; set; }
    // Captured at creation; changing preferences preserves previous in-app history.
    public bool showInApp { get; set; } = true;
    // Delivery state is separate from read/unread state.
    [Required, StringLength(20)] public string emailStatus { get; set; } = "NotRequested";
    public DateTime? emailAttemptedDate { get; set; }
    public DateTime? emailSentDate { get; set; }
    [StringLength(20)] public string? routineBillId { get; set; }
    [Column(TypeName="date")] public DateTime? billDueDate { get; set; }
    // Additional consent/deduplication metadata.
    [Required, StringLength(64)] public string eventKey { get; set; } = "";
    [StringLength(20)] public string? linkId { get; set; }
    public int? requestVersion { get; set; }
    [Column(TypeName="date")] public DateTime? sourceMonth { get; set; }
    [ForeignKey(nameof(userId))] public User? Receiver { get; set; }
    [ForeignKey(nameof(relatedUserId))] public User? RelatedUser { get; set; }
    [ForeignKey(nameof(linkId))] public ParentStudentLink? Link { get; set; }
}
