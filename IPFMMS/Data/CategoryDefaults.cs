namespace IPFMMS.Data
{
    public static class CategoryDefaults
    {
        public const int UnassignedOrder = 1000;

        public static IReadOnlyList<string> IncomeNames { get; } = Array.AsReadOnly(new[]
        {
            "Salary", "Allowance", "Scholarship", "Part-Time Job", "Freelance",
            "Investment Returns", "Gifts", "Other Income"
        });

        public static IReadOnlyList<string> ExpenseNames { get; } = Array.AsReadOnly(new[]
        {
            "Food & Drinks", "Transportation", "Education", "Shopping", "Entertainment",
            "Bills & Utilities", "Healthcare", "Loan Interest & Fees", "Other Expenses", "Debt Repayment", "Shared Savings"
        });
    }
}
