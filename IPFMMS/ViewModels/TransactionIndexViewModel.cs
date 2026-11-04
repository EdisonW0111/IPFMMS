namespace IPFMMS.ViewModels
{
    public class TransactionIndexViewModel
    {
        public DateTime SelectedMonth { get; set; }
        public List<TransactionViewModel> Transactions { get; set; }
            = new List<TransactionViewModel>();

        public decimal TotalIncome { get; set; }

        public decimal TotalExpenses { get; set; }

        public decimal TotalSavingsAllocated { get; set; }

        // These totals cover only SelectedMonth and exclude opening balances.
        public decimal NetBalance =>
            TotalIncome - TotalExpenses;

        public decimal MonthlyRemaining => NetBalance - TotalSavingsAllocated;
    }
}