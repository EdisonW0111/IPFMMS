using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IPFMMS.ViewModels
{
    public class CreateBudgetViewModel
    {
        // Expense category to assign the budget to.
        [Required(ErrorMessage = "Please select an expense category.")]
        [Display(Name = "Expense Category")]
        public string ExpenseCategoryId { get; set; }
            = string.Empty;

        // Format: yyyy-MM, for example 2026-11.
        [Required(ErrorMessage = "Please select a budget month.")]
        [RegularExpression(
            @"^\d{4}-(0[1-9]|1[0-2])$",
            ErrorMessage = "Please select a valid month.")]
        [Display(Name = "Budget Month")]
        public string BudgetMonth { get; set; }
            = DateTime.Today.ToString("yyyy-MM");

        // FixedAmount or Percentage.
        [Required(ErrorMessage = "Please select a budget type.")]
        [Display(Name = "Budget Type")]
        public string BudgetType { get; set; }
            = "FixedAmount";

        // Used for fixed-amount budgets.
        [Range(typeof(decimal), "0.01", "9999999999.99",
            ErrorMessage = "Budget amount must be greater than RM0.")]
        [Display(Name = "Budget Amount (RM)")]
        public decimal? BudgetAmount { get; set; }

        // Used for percentage-based budgets.
        [Range(typeof(decimal), "1", "100",
            ErrorMessage = "Percentage must be between 1% and 100%.")]
        [Display(Name = "Budget Percentage (%)")]
        public decimal? BudgetPercentage { get; set; }

        // An explicit opt-in to a separate category schedule.
        [Display(Name = "Repeat monthly")]
        public bool RepeatMonthly { get; set; }

        [Display(Name = "Repeat until (optional)")]
        public string? RepeatUntil { get; set; }

        // Optional notes.
        [StringLength(500,
            ErrorMessage = "Notes cannot exceed 500 characters.")]
        [Display(Name = "Notes (Optional)")]
        public string? Notes { get; set; }

        // Dropdown lists.
        public List<SelectListItem> ExpenseCategories { get; set; }
            = new List<SelectListItem>();

        public List<SelectListItem> BudgetTypes { get; set; }
            = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = "FixedAmount",
                    Text = "Fixed Amount (RM)"
                },
                new SelectListItem
                {
                    Value = "Percentage",
                    Text = "Percentage of Monthly Income (%)"
                }
            };
    }
}