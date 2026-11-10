using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace IPFMMS.Models
{
    public class User
    {
        [Key]
        [StringLength(20)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "User";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(20)]
        public string AccountStatus { get; set; } = IPFMMS.Models.AccountStatus.Pending;

        public int SecurityVersion { get; set; } = 1;

        // Chapter 4 attribute names share the existing persisted values. These aliases
        // add no database columns and keep existing serialization contracts unchanged.
        [NotMapped, JsonIgnore]
        public string userId { get => UserId; set => UserId = value; }
        [NotMapped, JsonIgnore]
        public string fullName { get => FullName; set => FullName = value; }
        [NotMapped, JsonIgnore]
        public string email { get => Email; set => Email = value; }
        [NotMapped, JsonIgnore]
        public string passwordHash { get => PasswordHash; set => PasswordHash = value; }
        [NotMapped, JsonIgnore]
        public string? phoneNumber { get => PhoneNumber; set => PhoneNumber = value; }
        [NotMapped, JsonIgnore]
        public string role { get => Role; set => Role = value; }
        [NotMapped, JsonIgnore]
        public DateTime createdDate { get => CreatedDate; set => CreatedDate = value; }
        [NotMapped, JsonIgnore]
        public string accountStatus { get => AccountStatus; set => AccountStatus = value; }
    }
}