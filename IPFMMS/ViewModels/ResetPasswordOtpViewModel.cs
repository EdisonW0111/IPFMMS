using System.ComponentModel.DataAnnotations;

namespace IPFMMS.ViewModels
{
    public class ResetPasswordOtpViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Verification code is required.")]
        [RegularExpression(@"^\d{6}$",
            ErrorMessage = "Please enter a valid 6-digit code.")]
        [Display(Name = "Verification Code")]
        public string OtpCode { get; set; } = string.Empty;

        public DateTime? ResendAvailableAtUtc { get; set; }

        public int ResendCooldownSeconds { get; set; }
    }
}
