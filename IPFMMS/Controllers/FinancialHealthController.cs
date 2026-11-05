using System.Globalization;
using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Controllers;
[Authorize(Roles="User,Parent")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class FinancialHealthController(FinancialHealthService health,IdGeneratorService ids):Controller
{
    private static DateTime Today=>DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
    private static bool Parse(string? value,DateTime today,out DateTime month){
        var current=new DateTime(today.Year,today.Month,1);month=current;
        return value==null||(DateTime.TryParseExact(value,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out month)&&month>=new DateTime(1000,4,1)&&month<=current);
    }
    [HttpGet]
    public async Task<IActionResult> Index(string? month=null,int page=1,CancellationToken ct=default){
        var owner=User.FindFirstValue(ClaimTypes.NameIdentifier);if(string.IsNullOrWhiteSpace(owner))return Challenge();
        var today=Today;
        if(!Parse(month,today,out var selected)){ModelState.AddModelError(string.Empty,"Choose a past or current valid month. Showing the current month.");selected=new(today.Year,today.Month,1);}
        ViewData["CurrentMonth"]=today.ToString("yyyy-MM");
        await health.RefreshHistoryAsync(owner,today,ids,ct);
        return View(await health.BuildAsync(owner,selected,today,page,ct));
    }
}
