using System.Globalization;
using System.Security.Claims;
using IPFMMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace IPFMMS.Controllers;
[Authorize(Roles = "User,Parent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PredictionController(SpendingPredictionService predictions) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? month = null, int page = 1, CancellationToken ct = default)
    {
        var owner = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(owner)) return Challenge();
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
        var current = new DateTime(today.Year, today.Month, 1);
        var selected = current;
        if (month != null && (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out selected) || selected < new DateTime(1000, 4, 1) || selected > current))
        {
            ModelState.AddModelError(string.Empty, "Choose a past or current prediction month. Future predictions need completed spending months. Showing the current month.");
            selected = current;
        }
        ViewData["CurrentMonth"] = current.ToString("yyyy-MM");
        return View(await predictions.BuildAndSaveAsync(owner, selected, today, page, ct));
    }
}
