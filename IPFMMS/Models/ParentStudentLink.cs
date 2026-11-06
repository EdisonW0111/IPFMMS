using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace IPFMMS.Models;

// Figure 4.24: original class and attributes retain their exact names.
public class ParentStudentLink
{
    [Key, StringLength(20)] public string linkId { get; set; } = "";
    [Required, StringLength(20)] public string parentId { get; set; } = "";
    [Required, StringLength(20)] public string userId { get; set; } = "";
    public DateTime linkedDate { get; set; }
    [Required, StringLength(20)] public string linkStatus { get; set; } = "Active";
    public DateTime? parentHiddenDate { get; set; }
    public DateTime? studentHiddenDate { get; set; }
    // Request/consent metadata supplements the five original diagram attributes.
    public DateTime? requestedDate { get; set; }
    public DateTime? acceptedDate { get; set; }
    public int requestVersion { get; set; }
    public DateTime? lastEmailAttemptDate { get; set; }
    public DateTime? emailSentDate { get; set; }
    [ForeignKey(nameof(parentId))] public User? Parent { get; set; }
    [ForeignKey(nameof(userId))] public User? Student { get; set; }
}
