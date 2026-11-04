using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IPFMMS.ViewModels
{
    public class EditExpenseViewModel
    {
        [Required]
        public string ExpenseId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select an expense category.")]
        [Display(Name = "Expense Category")]
        public string ExpenseCategoryId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter an amount.")]
        [Range(typeof(decimal), "0.01", "999999999.99",
            ErrorMessage = "Amount must be greater than RM0.")]
        [Display(Name = "Amount (RM)")]
        public decimal? Amount { get; set; }

        [Required(ErrorMessage = "Please select an expense date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Expense Date")]
        public DateTime? ExpenseDate { get; set; }

        [Required(ErrorMessage = "Please select a payment method.")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        [Display(Name = "Upload New Receipt")]
        public IFormFile? ReceiptFile { get; set; }

        [Display(Name = "Remove Existing Receipt")]
        public bool RemoveReceipt { get; set; }

        // Display-only property. Never trust this value for updates.
        public bool HasExistingReceipt { get; set; }

        public List<SelectListItem> ExpenseCategories { get; set; }
            = new List<SelectListItem>();

        public List<SelectListItem> PaymentMethods { get; set; }
            = new List<SelectListItem>();
    }
}