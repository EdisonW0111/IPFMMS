using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace IPFMMS.ViewModels;
public sealed class CreateParentStudentLinkViewModel
{
    [Required, StringLength(20), Display(Name = "Monitoring account")] public string ParentId { get; set; } = "";
    [Required, StringLength(20), Display(Name = "Account to be monitored")] public string UserId { get; set; } = "";
    public List<SelectListItem> Parents { get; set; } = new();
    public List<SelectListItem> Students { get; set; } = new();
}
public sealed class ParentStudentLinkRow
{
    public string LinkId { get; set; } = "";
    public string ParentId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ParentName { get; set; } = "";
    public string ParentEmail { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string StudentEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime LinkedDate { get; set; }
    public DateTime? RequestedDate { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public DateTime? ParentHiddenDate { get; set; }
    public DateTime? StudentHiddenDate { get; set; }
    public DateTime? EmailSentDate { get; set; }
    public int Version { get; set; }
    public string Stamp { get; set; } = "";
    public bool AccountsEligible { get; set; }
    public bool Hidden => ParentHiddenDate.HasValue || StudentHiddenDate.HasValue;
    public bool MonitoringAvailable => Status == "Active" && AcceptedDate.HasValue && AccountsEligible && !Hidden;
}
public sealed class ParentStudentLinkIndexViewModel
{
    public string Search { get; set; } = "";
    public string Status { get; set; } = "All";
    public int Page { get; set; } = 1;
    public int Count { get; set; }
    public int Pages => Math.Max(1, (Count + 19) / 20);
    public List<ParentStudentLinkRow> Links { get; set; } = new();
}
