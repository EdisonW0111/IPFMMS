using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using IPFMMS.Models;
namespace IPFMMS.ViewModels;
public class CreateRoutineBillViewModel
{
    [Required, StringLength(100), Display(Name="Bill or subscription name")] public string BillName { get; set; } = "";
    [Required, StringLength(30), Display(Name="Type")] public string BillType { get; set; } = "Bill";
    [Required, StringLength(20), Display(Name="Expense category")] public string ExpenseCategoryId { get; set; } = "";
    [Required, Range(typeof(decimal),"0.01","999999999.99"), Display(Name="Amount (RM)")] public decimal? Amount { get; set; }
    [Required, DataType(DataType.Date), Display(Name="Next unpaid due date")] public DateTime? DueDate { get; set; }
    [Required, StringLength(20), Display(Name="Payment frequency")] public string Frequency { get; set; } = "Monthly";
    [Required, StringLength(30), Display(Name="Payment method")] public string PaymentMethod { get; set; } = "";
    [Range(0,365), Display(Name="Remind me before (days)")] public int ReminderPeriod { get; set; } = 3;
    [StringLength(500)] public string? Description { get; set; }
    public List<SelectListItem> Categories { get; set; } = new();
}
public sealed class EditRoutineBillViewModel : CreateRoutineBillViewModel
{
    [Required, StringLength(20)] public string BillId { get; set; } = "";
    [Required, StringLength(64)] public string EditStamp { get; set; } = "";
    [Required, StringLength(20)] public string Status { get; set; } = "Active";
}
public sealed class DeleteRoutineBillViewModel
{
    public RoutineBill Bill { get; set; } = new();
    public string EditStamp { get; set; } = "";
}
public sealed class RoutineBillIndexViewModel
{
    public DateTime Today { get; set; }
    public int Count { get; set; }
    public int ActiveCount { get; set; }
    public int UpcomingCount { get; set; }
    public int OverdueCount { get; set; }
    public int Page { get; set; } = 1;
    public int Pages => Math.Max(1,(Count+19)/20);
    public List<RoutineBill> Bills { get; set; } = new();
    public string DisplayStatus(RoutineBill bill) => bill.status != "Active" ? bill.status : bill.dueDate < Today ? "Overdue" : bill.dueDate == Today ? "Due today" : "Upcoming";
}

public sealed class PayRoutineBillViewModel
{
    [Required, StringLength(20)] public string BillId { get; set; } = "";
    [Required, StringLength(64)] public string EditStamp { get; set; } = "";
    [Required, Range(typeof(decimal),"0.01","999999999.99"), Display(Name="Amount paid (RM)")] public decimal? Amount { get; set; }
    [Required, DataType(DataType.Date), Display(Name="Payment date")] public DateTime? PaidDate { get; set; }
    [Required, StringLength(30), Display(Name="Payment method")] public string PaymentMethod { get; set; } = "";
    public RoutineBill? Bill { get; set; }
}
public sealed class RoutineBillHistoryViewModel
{
    public string? Search { get; set; }
    public string? Month { get; set; }
    public string? Method { get; set; }
    public string? ExpenseId { get; set; }
    public int Page { get; set; } = 1;
    public int Count { get; set; }
    public int Pages => Math.Max(1,(Count+19)/20);
    public List<RoutineBillPayment> Payments { get; set; } = new();
}
