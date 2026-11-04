namespace IPFMMS.ViewModels
{
    public class TransactionViewModel
    {
        public string TransactionId { get; set; } = string.Empty;

        public string TransactionType { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime TransactionDate { get; set; }

        public string? Description { get; set; }

        public bool HasReceipt { get; set; }
    }
}