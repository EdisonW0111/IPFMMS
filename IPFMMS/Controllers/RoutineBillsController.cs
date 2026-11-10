using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Controllers;
[Authorize(Roles="User,Parent")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class RoutineBillsController(ApplicationDbContext db, RoutineBillService bills) : Controller
{
    private string Owner=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"";
    private static DateTime Today=>DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
    [HttpGet]
    public async Task<IActionResult> Index(int page=1,CancellationToken ct=default,string? search=null,string type="all",string status="all")
    {
        if(Owner=="")return Challenge();
        try
        {
            var model=await bills.ListAsync(Owner,Today,page,ct,search,type,status);
            return Request.Headers["X-Requested-With"]=="XMLHttpRequest"?PartialView("_ScheduledBillResults",model):View(model);
        }
        catch(InvalidOperationException ex){return BadRequest(ex.Message);}
    }
    private async Task PopulateAsync(CreateRoutineBillViewModel model,CancellationToken ct)
    {
        model.Categories=await db.ExpenseCategories.AsNoTracking().Where(c=>c.IsActive).OrderBy(c=>c.DisplayOrder).ThenBy(c=>c.CategoryName)
            .Select(c=>new SelectListItem(c.CategoryName,c.ExpenseCategoryId)).ToListAsync(ct);
    }
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        var model=new CreateRoutineBillViewModel{DueDate=Today};await PopulateAsync(model,ct);return View(model);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("BillName,BillType,ExpenseCategoryId,Amount,DueDate,Frequency,PaymentMethod,ReminderPeriod,Description")] CreateRoutineBillViewModel model,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        if(ModelState.IsValid)
        {
            try{await bills.CreateAsync(Owner,model,ct);TempData["SuccessMessage"]="Bill saved. Adding a bill does not record an expense or deduct money.";return RedirectToAction(nameof(Index));}
            catch(InvalidOperationException ex){ModelState.AddModelError(string.Empty,ex.Message);}
        }
        await PopulateAsync(model,ct);return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Edit(string id,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        var bill=await db.RoutineBills.AsNoTracking().SingleOrDefaultAsync(b=>b.billId==id&&b.userId==Owner&&b.status!="Deleted",ct);
        if(bill==null)return NotFound();
        var model=new EditRoutineBillViewModel
        {
            BillId=bill.billId,EditStamp=RoutineBillEditStamp.For(bill),BillName=bill.billName,BillType=bill.billType,
            ExpenseCategoryId=bill.expenseCategoryId,Amount=bill.amount,DueDate=bill.dueDate,Frequency=bill.frequency,
            PaymentMethod=bill.paymentMethod,ReminderPeriod=bill.reminderPeriod,Description=bill.description,Status=bill.status
        };
        await PopulateAsync(model,ct);return View(model);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id,[Bind("BillId,EditStamp,BillName,BillType,ExpenseCategoryId,Amount,DueDate,Frequency,PaymentMethod,ReminderPeriod,Description,Status")] EditRoutineBillViewModel model,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        if(id!=model.BillId)return BadRequest();
        if(!await db.RoutineBills.AnyAsync(b=>b.billId==id&&b.userId==Owner&&b.status!="Deleted",ct))return NotFound();
        if(ModelState.IsValid)
        {
            try
            {
                if(!await bills.EditAsync(Owner,model,ct))return NotFound();
                TempData["SuccessMessage"]="Bill updated. Existing payment history and expenses are unchanged.";return RedirectToAction(nameof(Index));
            }
            catch(InvalidOperationException ex){ModelState.AddModelError(string.Empty,ex.Message);}
        }
        await PopulateAsync(model,ct);return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Delete(string id,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        var bill=await db.RoutineBills.AsNoTracking().Include(b=>b.ExpenseCategory).SingleOrDefaultAsync(b=>b.billId==id&&b.userId==Owner&&b.status!="Deleted",ct);
        return bill==null?NotFound():View(new DeleteRoutineBillViewModel{Bill=bill,EditStamp=RoutineBillEditStamp.For(bill)});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id,string stamp,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        if(string.IsNullOrWhiteSpace(stamp))return BadRequest();
        try
        {
            if(!await bills.DeleteAsync(Owner,id,stamp,ct))return NotFound();
            TempData["SuccessMessage"]="Bill deleted from your list. Payment history and expenses are retained.";return RedirectToAction(nameof(Index));
        }
        catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;return RedirectToAction(nameof(Delete),new{id});}
    }


    [HttpGet]
    public async Task<IActionResult> Pay(string id,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        var bill=await db.RoutineBills.AsNoTracking().Include(b=>b.ExpenseCategory).SingleOrDefaultAsync(b=>b.billId==id&&b.userId==Owner&&b.status!="Deleted",ct);
        if(bill==null)return NotFound();
        if(bill.status!="Active"){TempData["WarningMessage"]="Only active bills can be marked as paid.";return RedirectToAction(nameof(Index));}
        if(bill.dueDate>=new DateTime(Today.Year,Today.Month,1).AddMonths(1)){TempData["WarningMessage"]="This bill is due in a future month. You can record it from the start of its due month.";return RedirectToAction(nameof(Index));}
        return View(new PayRoutineBillViewModel{BillId=id,EditStamp=RoutineBillEditStamp.For(bill),Amount=bill.amount,PaidDate=Today,PaymentMethod=bill.paymentMethod,Bill=bill});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string id,[Bind("BillId,EditStamp,Amount,PaidDate,PaymentMethod")] PayRoutineBillViewModel model,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        if(id!=model.BillId)return BadRequest();
        model.Bill=await db.RoutineBills.AsNoTracking().Include(b=>b.ExpenseCategory).SingleOrDefaultAsync(b=>b.billId==id&&b.userId==Owner&&b.status!="Deleted",ct);
        if(model.Bill==null)return NotFound();
        if(ModelState.IsValid)
        {
            try{if(!await bills.PayAsync(Owner,model,ct))return NotFound();TempData["SuccessMessage"]="Payment recorded as an expense. The bill schedule has been updated.";return RedirectToAction(nameof(History));}
            catch(InvalidOperationException ex){ModelState.AddModelError(string.Empty,ex.Message);}
        }
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> History(int page=1,CancellationToken ct=default,string? expenseId=null,string? search=null,string? month=null,string? method=null)
    {
        if(Owner=="")return Challenge();
        var query=db.RoutineBillPayments.AsNoTracking().Where(p=>p.Bill!.userId==Owner);
        if(!string.IsNullOrWhiteSpace(expenseId))query=query.Where(p=>p.expenseId==expenseId);
        search=search?.Trim();month=month?.Trim();method=method?.Trim();
        if(search?.Length>100||expenseId?.Length>20)return BadRequest("Search or expense reference is too long.");
        if(!string.IsNullOrEmpty(search))query=query.Where(p=>p.billNameSnapshot.Contains(search)||p.expenseId.Contains(search));
        if(!string.IsNullOrEmpty(month))
        {
            if(!DateTime.TryParseExact(month,"yyyy-MM",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var start)||start.Year<1000||start.Year>9998)return BadRequest("Choose a valid payment month.");
            var end=start.AddMonths(1);query=query.Where(p=>p.paidDate>=start&&p.paidDate<end);
        }
        if(!string.IsNullOrEmpty(method))
        {
            if(!IPFMMS.Models.RoutineBillOptions.PaymentMethods.Contains(method))return BadRequest("Choose a valid payment method.");
            query=query.Where(p=>p.paymentMethod==method);
        }
        var model=new RoutineBillHistoryViewModel{Search=search,Month=month,Method=method,ExpenseId=expenseId,Count=await query.CountAsync(ct)};
        model.Page=Math.Clamp(page,1,model.Pages);
        model.Payments=await query.OrderByDescending(p=>p.paidDate).ThenByDescending(p=>p.paymentId).Skip((model.Page-1)*20).Take(20).ToListAsync(ct);
        if(Request.Headers["X-Requested-With"]=="XMLHttpRequest")return PartialView("_HistoryResults",model);
        return View(model);
    }
}
