using System.ComponentModel.DataAnnotations;
namespace IPFMMS.ViewModels;
public sealed class SendMonitoringRequestViewModel
{
    [Required, EmailAddress, StringLength(255), Display(Name = "Email of the user you want to monitor")]
    public string Email { get; set; } = "";
}
public sealed class MonitoringLinkViewModel
{
    public string LinkId { get; set; } = "";
    public string RequesterId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public string RequesterEmail { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string StudentEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public int Version { get; set; }
    public DateTime? RequestedDate { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public DateTime? EmailSentDate { get; set; }
    public bool InvitationEmailEnabled { get; set; } = true;
    public string ViewerId { get; set; } = "";
    public bool IsRequester => ViewerId == RequesterId;
    public bool AccountsEligible { get; set; }
    public bool HasConsent => Status == "Active" && AcceptedDate.HasValue && AccountsEligible;
    public string OtherName => IsRequester ? StudentName : RequesterName;
    public string DisplayStatus => Status == "Active" && !AcceptedDate.HasValue ? "Consent required" : Status == "Active" && !AccountsEligible ? "Unavailable" : Status;
}
public sealed class MonitoringLinksIndexViewModel
{
    public string View { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int Count { get; set; }
    public int Pages => Math.Max(1, (Count + 19) / 20);
    public int ReceivedPending { get; set; }
    public List<MonitoringLinkViewModel> Links { get; set; } = new();
}
