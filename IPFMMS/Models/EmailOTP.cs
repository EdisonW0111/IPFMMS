using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
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

        // Chapter 4 attribute names share the existing persisted values. These aliases
        // add no database columns and keep existing serialization contracts unchanged.
        [NotMapped, JsonIgnore]
        public string otpId { get => OtpId; set => OtpId = value; }
        [NotMapped, JsonIgnore]
        public string userId { get => UserId; set => UserId = value; }
        [NotMapped, JsonIgnore]
        public string otpCode { get => OtpCode; set => OtpCode = value; }
        [NotMapped, JsonIgnore]
        public string purpose { get => Purpose; set => Purpose = value; }
        [NotMapped, JsonIgnore]
        public DateTime createdDateTime { get => CreatedDateTime; set => CreatedDateTime = value; }
        [NotMapped, JsonIgnore]
        public DateTime expiryDateTime { get => ExpiryDateTime; set => ExpiryDateTime = value; }
        [NotMapped, JsonIgnore]
        public bool isVerified { get => IsVerified; set => IsVerified = value; }
    }
}