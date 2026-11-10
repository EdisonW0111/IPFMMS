namespace IPFMMS.ViewModels;

public sealed class AdminAuditRow
{
    public string AuditId { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public string AdminUserId { get; set; } = "";
    public string AdminName { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
}
public sealed class AdminDashboardViewModel
{
    public DateTime AsOfUtc { get; set; } = DateTime.UtcNow;
    public int TotalAccounts { get; set; }
    public int ActiveUsers { get; set; }
    public int PendingAccounts { get; set; }
    public int InactiveAccounts { get; set; }
    public int DeletedAccounts { get; set; }
    public int AdminAccounts { get; set; }
    public int ActiveRelationships { get; set; }
    public int PendingRelationships { get; set; }
    public int TotalRelationships { get; set; }
    public int IncomeCategories { get; set; }
    public int ExpenseCategories { get; set; }
    public int ActiveIncomeCategories { get; set; }
    public int ActiveExpenseCategories { get; set; }
    public int IncomeRecords { get; set; }
    public int ExpenseRecords { get; set; }
    public int BudgetRecords { get; set; }
    public int SavingsGoals { get; set; }
    public int DebtRecords { get; set; }
    public int SharedGroups { get; set; }
    public List<AdminAuditRow> RecentActivity { get; set; } = new();
    public List<AdminUserRow> RecentAccounts { get; set; } = new();
}
