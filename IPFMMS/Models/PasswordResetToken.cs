using System.ComponentModel.DataAnnotations;

namespace IPFMMS.Models
{
    public class PasswordResetToken
    {
        [Key]
        [StringLength(36)]
        public string TokenId { get; set; } =
            Guid.NewGuid().ToString();

        [Required]
        [StringLength(20)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedDateTime { get; set; }
            = DateTime.UtcNow;

        public DateTime ExpiryDateTime { get; set; }

        public bool IsUsed { get; set; } = false;

        public User User { get; set; } = null!;
    }
}
