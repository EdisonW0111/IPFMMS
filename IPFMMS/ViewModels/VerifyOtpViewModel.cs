using System.ComponentModel.DataAnnotations;

namespace IPFMMS.ViewModels
{
    public class VerifyOtpViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Verification code is required.")]
        [RegularExpression(
            @"^\d{6}$",
            ErrorMessage = "Please enter a valid 6-digit code.")]
        [Display(Name = "Verification Code")]
        public string OtpCode { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int ResendCooldownSeconds { get; set; }

        public DateTime? ResendAvailableAtUtc { get; set; }
    }
}