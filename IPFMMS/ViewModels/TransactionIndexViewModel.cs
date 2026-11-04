namespace IPFMMS.ViewModels
{
    public class TransactionIndexViewModel
    {
        public List<TransactionViewModel> Transactions { get; set; }
            = new List<TransactionViewModel>();

        public decimal TotalIncome { get; set; }

        public decimal TotalExpenses { get; set; }

        public decimal NetBalance =>
            TotalIncome - TotalExpenses;
    }
}