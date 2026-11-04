using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IPFMMS.ViewModels
{
    public class EditIncomeViewModel
    {
        [Required]
        public string IncomeId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select an income category.")]
        [Display(Name = "Income Category")]
        public string IncomeCategoryId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter an amount.")]
        [Range(typeof(decimal), "0.01", "999999999.99",
            ErrorMessage = "Amount must be greater than RM0.")]
        [Display(Name = "Amount (RM)")]
        public decimal? Amount { get; set; }

        [Required(ErrorMessage = "Please select an income date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Income Date")]
        public DateTime? IncomeDate { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        public List<SelectListItem> IncomeCategories { get; set; }
            = new List<SelectListItem>();
    }
}