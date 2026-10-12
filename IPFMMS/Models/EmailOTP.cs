using System.ComponentModel.DataAnnotations;

namespace IPFMMS.Models
{
    public class EmailOTP
    {
        [Key]
        [StringLength(20)]
        public string OtpId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string OtpCode { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Purpose { get; set; } = string.Empty;

        public DateTime CreatedDateTime { get; set; }
            = DateTime.UtcNow;

        public DateTime ExpiryDateTime { get; set; }

        public bool IsVerified { get; set; } = false;

        public int FailedAttempts { get; set; } = 0;

        public User User { get; set; } = null!;
    }
}