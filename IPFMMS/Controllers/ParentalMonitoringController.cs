using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Controllers;
[Authorize(Roles = "User,Parent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ParentalMonitoringController(ApplicationDbContext db, MonitoringLinkService links, MonitoringSummaryService summaries) : Controller
{
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private IQueryable<MonitoringLinkViewModel> Rows(string owner) => db.ParentStudentLinks.AsNoTracking()
        .Where(l => l.parentId == owner || l.userId == owner).Select(l => new MonitoringLinkViewModel
        {
            LinkId = l.linkId, RequesterId = l.parentId, StudentId = l.userId,
            RequesterName = l.Parent!.FullName, RequesterEmail = l.Parent.Email,
            StudentName = l.Student!.FullName, StudentEmail = l.Student.Email,
            Status = l.linkStatus, Version = l.requestVersion, RequestedDate = l.requestedDate,
            AccountsEligible = l.Parent.AccountStatus == "Active" && l.Student.AccountStatus == "Active"
                && (l.Parent.Role == "User" || l.Parent.Role == "Parent") && (l.Student.Role == "User" || l.Student.Role == "Parent"),
            AcceptedDate = l.acceptedDate, EmailSentDate = l.emailSentDate, ViewerId = owner
        });

    [HttpGet]
    public async Task<IActionResult> Index(string view = "all", int page = 1, CancellationToken ct = default)
    {
        if (Owner == "") return Challenge();
        if (view != "all" && view != "received" && view != "sent" && view != "monitoring") view = "all";
        var query = Rows(Owner);
        if (view == "received") query = query.Where(l => l.StudentId == Owner && l.Status == "Pending");
        if (view == "sent") query = query.Where(l => l.RequesterId == Owner);
        if (view == "monitoring") query = query.Where(l => l.RequesterId == Owner && l.Status == "Active" && l.AcceptedDate != null && l.AccountsEligible);
        var model = new MonitoringLinksIndexViewModel { View = view, Count = await query.CountAsync(ct),
            ReceivedPending = await db.ParentStudentLinks.CountAsync(l => l.userId == Owner && l.linkStatus == "Pending", ct) };
        model.Page = Math.Clamp(page, 1, model.Pages);
        model.Links = await query.OrderByDescending(l => l.RequestedDate).ThenBy(l => l.LinkId).Skip((model.Page - 1) * 20).Take(20).ToListAsync(ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Summary(string student, string? month = null, CancellationToken ct = default)
    {
        if (Owner == "") return Challenge();
        if (string.IsNullOrWhiteSpace(student)) return NotFound();
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
        var current = new DateTime(today.Year, today.Month, 1);
        var selected = current;
        if (month != null && (!DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out selected)
            || selected < new DateTime(1000, 4, 1) || selected > current))
        {
            ModelState.AddModelError(string.Empty, "Choose a past or current valid month. Showing the current month.");
            selected = current;
        }
        var model = await summaries.BuildAsync(Owner, student, selected, today, ct);
        if (model == null) return NotFound();
        ViewData["CurrentMonth"] = current.ToString("yyyy-MM");
        return View(model);
    }

    [HttpGet] public IActionResult SendRequest() => Owner == "" ? Challenge() : View(new SendMonitoringRequestViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequest(SendMonitoringRequestViewModel model, CancellationToken ct = default)
    {
        if (Owner == "") return Challenge();
        if (!ModelState.IsValid) return View(model);
        try
        {
            bool sent = await links.RequestAsync(Owner, model.Email, ct);
            TempData[sent ? "SuccessMessage" : "WarningMessage"] = sent
                ? "Request sent. Monitoring starts only after the other user accepts."
                : "Your request was saved, but its email could not be sent. The recipient can still review it in Parental Monitoring. You can retry the email after one minute.";
            return RedirectToAction(nameof(Index), new { view = "sent" });
        }
        catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(model); }
    }

    [HttpGet]
    public async Task<IActionResult> Review(string id, CancellationToken ct = default)
    {
        if (Owner == "") return Challenge();
        var model = await Rows(Owner).SingleOrDefaultAsync(l => l.LinkId == id, ct);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(string id, int version, string response, CancellationToken ct = default)
    {
        if (Owner == "") return Challenge();
        if (response != "Accept" && response != "Reject") return BadRequest();
        return await Change(id, version, response, ct);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(string id, int version, CancellationToken ct = default) => Change(id, version, "Cancel", ct);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Disconnect(string id, int version, CancellationToken ct = default) => Change(id, version, "Disconnect", ct);
    private async Task<IActionResult> Change(string id, int version, string action, CancellationToken ct)
    {
        if (Owner == "") return Challenge();
        try
        {
            if (!await links.TransitionAsync(Owner, id, version, action, ct)) return NotFound();
            TempData["SuccessMessage"] = action switch { "Accept" => "Request accepted. This user may now monitor your financial summaries.", "Reject" => "Request rejected. No access was granted.", "Cancel" => "Request cancelled.", _ => "Disconnected. Monitoring access has ended." };
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex) { TempData["WarningMessage"] = ex.Message; return RedirectToAction(nameof(Review), new { id }); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(string id, int version, CancellationToken ct = default)
    {
        if (Owner == "") return Challenge();
        try
        {
            var sent = await links.ResendAsync(Owner, id, version, ct);
            if (sent == null) return NotFound();
            TempData[sent.Value ? "SuccessMessage" : "WarningMessage"] = sent.Value ? "Request email sent again." : "Email delivery failed. The request remains available for in-app review. Retry after one minute.";
        }
        catch (InvalidOperationException ex) { TempData["WarningMessage"] = ex.Message; }
        return RedirectToAction(nameof(Review), new { id });
    }
}
