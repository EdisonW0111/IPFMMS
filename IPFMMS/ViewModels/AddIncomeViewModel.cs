using IPFMMS.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IPFMMS.ViewModels
{
    public class AddIncomeViewModel
    {
        [Required(ErrorMessage = "Income category is required.")]
        [Display(Name = "Income Category")]
        public string IncomeCategoryId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(typeof(decimal), "0.01", "999999999.99",
            ErrorMessage = "Amount must be greater than RM0.")]
        [Display(Name = "Amount (RM)")]
        public decimal? Amount { get; set; }

        [Required(ErrorMessage = "Income date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Income Date")]
        public DateTime? IncomeDate { get; set; }
            = DateTime.Today;

        [StringLength(255,
            ErrorMessage = "Description cannot exceed 255 characters.")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        // Used to populate the income category dropdown.
        [Required(ErrorMessage = "Please select a payment method.")]
        [StringLength(30)]
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }

        [StringLength(100, ErrorMessage = "Reference cannot exceed 100 characters.")]
        [Display(Name = "Reference (Optional)")]
        public string? Reference { get; set; }

        public List<SelectListItem> PaymentMethods { get; set; } =
            IncomePaymentMethods.Values.Select(value => new SelectListItem
            {
                Value = value, Text = value
            }).ToList();

        public List<SelectListItem> IncomeCategories { get; set; }
            = new List<SelectListItem>();
    }
}