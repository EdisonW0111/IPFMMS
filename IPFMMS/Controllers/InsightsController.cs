using System.Globalization;
using System.Security.Claims;
using IPFMMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace IPFMMS.Controllers;

[Authorize(Roles="User,Parent")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class InsightsController(SpendingInsightsService insights):Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? month=null,CancellationToken ct=default)
    {
        var owner=User.FindFirstValue(ClaimTypes.NameIdentifier);
        if(string.IsNullOrWhiteSpace(owner))return Challenge();
        var today=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
        var current=new DateTime(today.Year,today.Month,1);var selected=current;
        if(month!=null){
            if(!DateTime.TryParseExact(month,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)||parsed<new DateTime(1000,2,1)||parsed>current)
                ModelState.AddModelError(string.Empty,"Choose a past or current month from February 1000 onward. Showing the current month.");
            else selected=parsed;
        }
        ViewData["CurrentMonth"]=current.ToString("yyyy-MM",CultureInfo.InvariantCulture);
        var model=await insights.BuildAsync(owner,selected,today,ct);
        return Request.Headers["X-Requested-With"]=="XMLHttpRequest"?PartialView("_Insights",model):View(model);
    }
}
