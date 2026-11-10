using System.Security.Claims;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPFMMS.Controllers;

[Authorize(Roles = "User,Parent,Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SettingsController(UserPreferenceService preferences) : Controller
{
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        if (Owner == "") return Challenge();
        var saved = await preferences.GetAsync(Owner, ct);
        return View(new SettingsViewModel {
            Theme = saved.Theme, Revision = saved.Revision,
            BillInApp = saved.BillInApp, BillEmail = saved.BillEmail,
            MonitoringInApp = saved.MonitoringInApp, MonitoringEmail = saved.MonitoringEmail,
            InvitationEmail = saved.InvitationEmail
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SettingsViewModel model, CancellationToken ct = default)
    {
        if (Owner == "") return Challenge();
        if (!ModelState.IsValid) return View(model);
        if (User.IsInRole("Admin"))
        {
            // Admin Settings exposes appearance only; do not overwrite retained user-channel choices.
            var saved = await preferences.GetAsync(Owner, ct);
            model.BillInApp = saved.BillInApp; model.BillEmail = saved.BillEmail;
            model.MonitoringInApp = saved.MonitoringInApp; model.MonitoringEmail = saved.MonitoringEmail;
            model.InvitationEmail = saved.InvitationEmail;
        }
        try { await preferences.SaveAsync(Owner, model, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); return View(model); }
        Response.Cookies.Append("IPFMMS.Theme", model.Theme, new CookieOptions {
            HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps, MaxAge = TimeSpan.FromDays(365), Path = "/"
        });
        TempData["SuccessMessage"] = "Your settings have been saved.";
        return RedirectToAction(nameof(Index));
    }
}
