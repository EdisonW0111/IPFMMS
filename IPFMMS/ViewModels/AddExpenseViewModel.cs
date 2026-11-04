using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IPFMMS.ViewModels
{
    public class AddExpenseViewModel
    {
        [Required(ErrorMessage = "Expense category is required.")]
        [Display(Name = "Expense Category")]
        public string ExpenseCategoryId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(typeof(decimal), "0.01", "999999999.99",
            ErrorMessage = "Amount must be greater than RM0.")]
        [Display(Name = "Amount (RM)")]
        public decimal? Amount { get; set; }

        [Required(ErrorMessage = "Expense date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Expense Date")]
        public DateTime? ExpenseDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Payment method is required.")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(255,
            ErrorMessage = "Description cannot exceed 255 characters.")]
        public string? Description { get; set; }

        [Display(Name = "Receipt (Optional)")]
        public IFormFile? ReceiptFile { get; set; }

        // Dropdown data populated by the controller.
        public List<SelectListItem> ExpenseCategories { get; set; }
            = new List<SelectListItem>();

        public List<SelectListItem> PaymentMethods { get; set; }
            = new List<SelectListItem>();
    }
}