using System.Security.Claims;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Controllers;
[Authorize(Roles="User,Parent")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class NotificationsController(MonitoringAlertService alerts, NotificationService notifications) : Controller
{
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private static DateTime Today => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
    [HttpGet]
    public async Task<IActionResult> Index(string type="all", string read="all", int page=1, CancellationToken ct=default)
    {
        if (Owner == "") return Challenge();
        await alerts.RefreshAsync(Owner, Today, ct);
        if (type != "all" && type != "BudgetOverspending" && type != "LowFinancialHealth") type="all";
        if (read != "all" && read != "unread" && read != "read") read="all";
        var query=notifications.Visible(Owner);
        if (type != "all") query=query.Where(n=>n.notificationType==type);
        if (read != "all") query=query.Where(n=>n.isRead==(read=="read"));
        var model=new NotificationsViewModel {Type=type,Read=read,Count=await query.CountAsync(ct),UnreadCount=await notifications.UnreadAsync(Owner,ct)};
        model.Page=Math.Clamp(page,1,model.Pages);
        model.Notifications=await query.OrderByDescending(n=>n.createdDateTime).ThenByDescending(n=>n.notificationId)
            .Skip((model.Page-1)*20).Take(20).ToListAsync(ct);
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> UnreadCount(CancellationToken ct=default)
    {
        if (Owner == "") return Challenge();
        await alerts.RefreshAsync(Owner,Today,ct);
        return Json(new {count=await notifications.UnreadAsync(Owner,ct)});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRead(string id,bool read,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        if(!await notifications.SetReadAsync(Owner,id,read,ct))return NotFound();
        return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        await notifications.MarkAllReadAsync(Owner,ct);
        return RedirectToAction(nameof(Index));
    }
}
