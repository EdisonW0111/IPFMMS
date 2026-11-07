using System.Security.Claims;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Controllers;
[Authorize(Roles="User,Parent")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class NotificationsController(MonitoringAlertService alerts, NotificationService notifications, NotificationEmailService email, RoutineBillReminderService bills) : Controller
{
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private static DateTime Today => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
    [HttpGet]
    public async Task<IActionResult> Index(string type="all", string read="all", int page=1, CancellationToken ct=default)
    {
        if (Owner == "") return Challenge();
        await bills.RefreshAsync(Owner,Today,ct);
        await alerts.RefreshAsync(Owner, Today, ct);
        if (type != "all" && type != "BudgetOverspending" && type != "LowFinancialHealth" && type != "BillUpcoming" && type != "BillDueToday" && type != "BillOverdue") type="all";
        if (read != "all" && read != "unread" && read != "read") read="all";
        var query=notifications.Visible(Owner);
        if (type != "all") query=query.Where(n=>n.notificationType==type);
        if (read != "all") query=query.Where(n=>n.isRead==(read=="read"));
        var model=new NotificationsViewModel {Type=type,Read=read,Count=await query.CountAsync(ct),UnreadCount=await notifications.UnreadAsync(Owner,ct)};
        model.Page=Math.Clamp(page,1,model.Pages);
        model.Notifications=await query.OrderByDescending(n=>n.createdDateTime).ThenByDescending(n=>n.notificationId)
            .Skip((model.Page-1)*20).Take(20).ToListAsync(ct);
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView("_NotificationResults", model) : View(model);
    }
    [HttpGet]
    public async Task<IActionResult> UnreadCount(CancellationToken ct=default)
    {
        if (Owner == "") return Challenge();
        await bills.RefreshAsync(Owner,Today,ct);
        await alerts.RefreshAsync(Owner,Today,ct);
        return Json(new {count=await notifications.UnreadAsync(Owner,ct)});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRead(string id,bool read,string type="all",string filterRead="all",int page=1,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        if(!await notifications.SetReadAsync(Owner,id,read,ct))return NotFound();
        return RedirectToAction(nameof(Index), new {type,read=filterRead,page});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string type="all",string filterRead="all",int page=1,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        await notifications.MarkAllReadAsync(Owner,ct);
        return RedirectToAction(nameof(Index), new {type,read=filterRead,page});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(string id,string type="all",string filterRead="all",int page=1,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        if(!await email.QueueRetryAsync(Owner,id,ct))return NotFound();
        return RedirectToAction(nameof(Index),new{type,read=filterRead,page});
    }

}
