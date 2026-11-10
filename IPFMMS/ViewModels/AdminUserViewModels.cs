using System.ComponentModel.DataAnnotations;
namespace IPFMMS.ViewModels;

public sealed class AdminUserRow
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "";
    public string AccountStatus { get; set; } = "";
    public DateTime CreatedDate { get; set; }
}
public sealed class AdminUserIndexViewModel
{
    public string Search { get; set; } = "";
    public string Status { get; set; } = "All";
    public string Role { get; set; } = "All";
    public int Count { get; set; }
    public int Page { get; set; } = 1;
    public int Pages => Math.Max(1, (Count + 19) / 20);
    public List<AdminUserRow> Users { get; set; } = new();
}
public sealed class AdminUserEditViewModel
{
    [Required, StringLength(20)] public string UserId { get; set; } = "";
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    [Phone, StringLength(20)] public string? PhoneNumber { get; set; }
    [Required, StringLength(20)] public string Role { get; set; } = "User";
    [Required, StringLength(20)] public string AccountStatus { get; set; } = "Active";
    [Required, StringLength(64)] public string EditStamp { get; set; } = "";
    public bool IsSelf { get; set; }
    public bool IsPending { get; set; }
    public bool IsDeleted { get; set; }
}
public sealed class AdminUserDetailsViewModel
{
    public AdminUserRow User { get; set; } = new();
    public bool IsSelf { get; set; }
    public bool CanEdit => User.AccountStatus != "Deleted";
    public bool CanDelete => !IsSelf && User.AccountStatus != "Deleted";
    public string EditStamp { get; set; } = "";
    public int IncomeCount { get; set; }
    public int ExpenseCount { get; set; }
    public int BudgetCount { get; set; }
    public int SavingsGoalCount { get; set; }
    public int DebtCount { get; set; }
    public int SharedFinanceCount { get; set; }
    public int MonitoringAsParentCount { get; set; }
    public int MonitoringAsStudentCount { get; set; }
    public List<AdminAuditRow> AuditEntries { get; set; } = new();
}
public sealed class AdminUserDeleteViewModel
{
    public AdminUserRow User { get; set; } = new();
    [Required, StringLength(64)] public string EditStamp { get; set; } = "";
}
