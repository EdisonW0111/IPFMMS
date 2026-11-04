using System;
using System.ComponentModel.DataAnnotations;

namespace IPFMMS.ViewModels
{
    public class TransactionFilterViewModel
    {
        // Search by transaction ID, category or description.
        [StringLength(100)]
        public string? Search { get; set; }

        // All, Income or Expense.
        public string TransactionType { get; set; } = "All";

        // Optional category filter.
        public string? CategoryId { get; set; }

        // Optional date range.
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        // Optional amount range.
        [Range(typeof(decimal), "0", "999999999.99")]
        public decimal? MinAmount { get; set; }

        [Range(typeof(decimal), "0", "999999999.99")]
        public decimal? MaxAmount { get; set; }

        // Newest, Oldest, HighestAmount, LowestAmount.
        public string SortBy { get; set; } = "Newest";

        // Pagination.
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 50)]
        public int PageSize { get; set; } = 10;
    }
}