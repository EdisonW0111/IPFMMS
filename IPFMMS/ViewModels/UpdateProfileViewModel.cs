using System.ComponentModel.DataAnnotations;

namespace IPFMMS.ViewModels
{
    public class UpdateProfileViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(
            100,
            ErrorMessage = "Full name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(
            20,
            ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }
}