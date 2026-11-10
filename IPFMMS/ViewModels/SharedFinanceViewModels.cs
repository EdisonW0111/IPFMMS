using System.ComponentModel.DataAnnotations;
using IPFMMS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace IPFMMS.ViewModels;
public sealed class SharedFinanceFormViewModel
{
    public string? Id { get; set; }
    public int Revision { get; set; }
    [Required,StringLength(100),Display(Name="Shared finance name")] public string Name { get; set; } = "";
    public string FinanceType { get; set; } = "MonthlyBudget";
    [Required,Range(typeof(decimal),"0.01","999999999.99"),Display(Name="Budget / savings target (RM)")] public decimal? Amount { get; set; }
    public string? Month { get; set; } = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM");
    [StringLength(500)] public string? Description { get; set; }
    [StringLength(3000),Display(Name="Invite registered users by email")] public string? InviteEmails { get; set; }
    public string Status { get; set; } = "Active";
}
public sealed class SharedFinanceIndexViewModel
{
    public string? Search { get; set; }
    public string Type { get; set; } = "all";
    public string Scope { get; set; } = "all";
    public int Count { get; set; }
    public int Page { get; set; } = 1;
    public int Pages=>Math.Max(1,(Count+11)/12);
    public string Owner { get; set; } = "";
    public List<SharedFinance> Groups { get; set; } = new();
    public Dictionary<string,int> MemberCounts { get; set; } = new();
    public HashSet<string> OwnedGroupIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SharedFinanceMember> Invitations { get; set; } = new();
}
public sealed class SharedFinanceDetailsViewModel
{
    public SharedFinance Finance { get; set; } = new();
    public string Owner { get; set; } = "";
    public bool IsOwner { get; set; }
    public int ActiveOwnerCount { get; set; }
    public bool CanLeave => !IsOwner || ActiveOwnerCount > 1;
    public string Month { get; set; } = "";
    public decimal BudgetAmount { get; set; }
    public decimal Spent { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal Saved { get; set; }
    public decimal MySaved { get; set; }
    public int Count { get; set; }
    public int Page { get; set; } = 1;
    public int Pages=>Math.Max(1,(Count+19)/20);
    public string TransactionType { get; set; } = "all";
    public List<SharedFinanceTransaction> Transactions { get; set; } = new();
    public List<SharedFinanceMember> Members { get; set; } = new();
}
public sealed class SharedEntryViewModel
{
    [Required,StringLength(20)] public string Id { get; set; } = "";
    [Required,StringLength(32)] public string SubmissionKey { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "Expense";
    [Required,Range(typeof(decimal),"0.01","999999999.99")] public decimal? Amount { get; set; }
    [Required,DataType(DataType.Date)] public DateTime? Date { get; set; } = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
    public string? CategoryId { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    [StringLength(500)] public string? Description { get; set; }
    public SharedFinance? Finance { get; set; }
    public List<SelectListItem> Categories { get; set; } = new();
}
public sealed class SharedInvitationViewModel
{
    public string Id { get; set; } = "";
    [Required,EmailAddress,StringLength(255)] public string Email { get; set; } = "";
    public SharedFinance? Finance { get; set; }
}
public sealed class SharedPersonalViewModel
{
    public int Count { get; set; }
    public int Page { get; set; } = 1;
    public int Pages=>Math.Max(1,(Count+19)/20);
    public List<SharedFinanceTransaction> Entries { get; set; } = new();
    public List<SharedFinance> SavingsGroups { get; set; } = new();
    public Dictionary<string,decimal> Balances { get; set; } = new();
}

public sealed class PromoteSharedOwnerViewModel
{
    [Required,StringLength(20)] public string Id { get; set; } = "";
    [Required,StringLength(20)] public string UserId { get; set; } = "";
    [Range(1,int.MaxValue)] public int Revision { get; set; }
    [Range(1,int.MaxValue)] public int InvitationVersion { get; set; }
    public string MemberName { get; set; } = "";
    public string GroupName { get; set; } = "";
}
