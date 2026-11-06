using IPFMMS.Models;
namespace IPFMMS.ViewModels;
public sealed class NotificationsViewModel
{
    public string Type { get; set; } = "all";
    public string Read { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int Count { get; set; }
    public int UnreadCount { get; set; }
    public int Pages => Math.Max(1, (Count + 19) / 20);
    public List<Notification> Notifications { get; set; } = new();
}
