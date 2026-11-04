using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IPFMMS.ViewModels
{
    public class AllTransactionsViewModel
    {
        // Current filters selected by the user.
        public TransactionFilterViewModel Filter { get; set; }
            = new TransactionFilterViewModel();

        // Transactions returned by the server.
        public List<TransactionViewModel> Transactions { get; set; }
            = new List<TransactionViewModel>();

        // Dropdown options.
        public List<SelectListItem> TransactionTypes { get; set; }
            = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = "All",
                    Text = "All Transactions"
                },
                new SelectListItem
                {
                    Value = "Income",
                    Text = "Income"
                },
                new SelectListItem
                {
                    Value = "Expense",
                    Text = "Expense"
                }
            };

        public List<SelectListItem> Categories { get; set; }
            = new List<SelectListItem>();

        public List<SelectListItem> SortOptions { get; set; }
            = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = "Newest",
                    Text = "Newest First"
                },
                new SelectListItem
                {
                    Value = "Oldest",
                    Text = "Oldest First"
                },
                new SelectListItem
                {
                    Value = "HighestAmount",
                    Text = "Highest Amount"
                },
                new SelectListItem
                {
                    Value = "LowestAmount",
                    Text = "Lowest Amount"
                }
            };

        // Pagination information.
        public int TotalRecords { get; set; }

        public int TotalPages =>
            Filter.PageSize > 0
                ? (int)Math.Ceiling(
                    (double)TotalRecords / Filter.PageSize)
                : 0;

        public bool HasPreviousPage => Filter.Page > 1;

        public bool HasNextPage => Filter.Page < TotalPages;
    }
}