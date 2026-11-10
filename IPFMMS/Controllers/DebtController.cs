using System.Security.Claims;
using System.Globalization;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Controllers;
[Authorize(Roles="User,Parent")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class DebtController(ApplicationDbContext db,DebtService service):Controller
{
    private string Owner=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"";
    [HttpGet]
    public async Task<IActionResult> Index(string? search=null,string status="all",string? type=null,int page=1,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();search=search?.Trim();
        if(search?.Length>100)return BadRequest("Search is too long.");
        if(!new[]{"all","Active","Inactive","Paid","Overdue"}.Contains(status))return BadRequest("Choose a valid status.");
        if(!string.IsNullOrEmpty(type)&&!DebtOptions.Types.Contains(type))return BadRequest("Choose a valid debt type.");
        var all=db.Debts.AsNoTracking().Where(d=>d.userId==Owner&&d.status!="Deleted");
        var active=all.Where(d=>d.status=="Active");
        var model=new DebtIndexViewModel{Search=search,Status=status,Type=type,
            Outstanding=await all.SumAsync(d=>(decimal?)d.outstandingAmount,ct)??0,
            TotalPaid=await all.SumAsync(d=>(decimal?)(d.originalAmount-d.outstandingAmount),ct)??0,
            MonthlyRepayment=await active.SumAsync(d=>(decimal?)d.monthlyRepaymentAmount,ct)??0,
            ActiveCount=await active.CountAsync(ct),UpcomingDate=await active.MinAsync(d=>(DateTime?)d.dueDate,ct)};
        var query=all;
        if(!string.IsNullOrEmpty(search))query=query.Where(d=>d.debtName.Contains(search));
        if(!string.IsNullOrEmpty(type))query=query.Where(d=>d.debtType==type);
        if(status=="Overdue"){var today=DebtOptions.Today;query=query.Where(d=>d.status=="Active"&&d.dueDate<today);}
        else if(status!="all")query=query.Where(d=>d.status==status);
        model.Count=await query.CountAsync(ct);model.Page=Math.Clamp(page,1,model.Pages);
        model.Debts=await query.OrderBy(d=>d.status=="Active"?0:d.status=="Inactive"?1:2).ThenBy(d=>d.dueDate).ThenBy(d=>d.debtId).Skip((model.Page-1)*20).Take(20).ToListAsync(ct);
        return Request.Headers["X-Requested-With"]=="XMLHttpRequest"?PartialView("_DebtResults",model):View(model);
    }
    [HttpGet]
    public IActionResult Create()=>Owner==""?Challenge():View("Form",new DebtFormViewModel{CalculationMode="Calculated",LoanTermMonths=60,DueDate=DebtOptions.Today.AddMonths(1)});
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("DebtName,DebtType,OriginalAmount,OutstandingAmount,MonthlyRepaymentAmount,InterestRate,DueDate,Description,CalculationMode,RateType,IsExistingLoan,LoanTermMonths,LoanStartDate,ReferenceRate,RateSpread,ReferenceRateName")]DebtFormViewModel model,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();
        ClearDerivedErrors(model);
        if(ModelState.IsValid)try{var id=await service.SaveAsync(Owner,model,false,ct);TempData["SuccessMessage"]="Debt saved. No income or expense was created.";return RedirectToAction(nameof(Details),new{id});}catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
        return View("Form",model);
    }
    [HttpGet]
    public async Task<IActionResult> Edit(string id,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();var d=await db.Debts.AsNoTracking().SingleOrDefaultAsync(d=>d.debtId==id&&d.userId==Owner&&d.status!="Deleted",ct);if(d==null)return NotFound();
        return View("Form",new DebtFormViewModel{DebtId=id,Revision=d.revision,DebtName=d.debtName,DebtType=d.debtType,OriginalAmount=d.originalAmount,OutstandingAmount=d.outstandingAmount,MonthlyRepaymentAmount=d.monthlyRepaymentAmount,InterestRate=d.interestRate,DueDate=d.dueDate,Description=d.description,Status=d.status,CalculationMode=d.calculationMode,RateType=d.rateType,IsExistingLoan=d.isExistingLoan,LoanTermMonths=d.loanTermMonths,LoanStartDate=d.loanStartDate,ReferenceRate=d.referenceRate,RateSpread=d.rateSpread,ReferenceRateName=d.referenceRateName??"SBR",HasRepayments=await db.DebtRepayments.AnyAsync(p=>p.debtId==id,ct)});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id,[Bind("DebtId,Revision,DebtName,DebtType,OriginalAmount,OutstandingAmount,MonthlyRepaymentAmount,InterestRate,DueDate,Description,Status,CalculationMode,RateType,IsExistingLoan,LoanTermMonths,LoanStartDate,ReferenceRate,RateSpread,ReferenceRateName")]DebtFormViewModel model,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();if(id!=model.DebtId)return BadRequest();
        if(!await db.Debts.AnyAsync(d=>d.debtId==id&&d.userId==Owner&&d.status!="Deleted",ct))return NotFound();
        ClearDerivedErrors(model);
        if(ModelState.IsValid)try{if(await service.SaveAsync(Owner,model,true,ct)==null)return NotFound();TempData["SuccessMessage"]="Debt updated. Existing repayments and expenses are unchanged.";return RedirectToAction(nameof(Details),new{id});}catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
        model.HasRepayments=await db.DebtRepayments.AnyAsync(p=>p.debtId==id,ct);return View("Form",model);
    }
    [HttpGet]
    public async Task<IActionResult> Details(string id,int page=1,string? month=null,string? expenseId=null,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();var d=await db.Debts.AsNoTracking().SingleOrDefaultAsync(d=>d.debtId==id&&d.userId==Owner,ct);if(d==null)return NotFound();
        var all=db.DebtRepayments.AsNoTracking().Where(p=>p.debtId==id);
        var model=new DebtDetailsViewModel{Debt=d,Month=month,ExpenseId=expenseId,RecordedPaid=await all.SumAsync(p=>(decimal?)p.repaymentAmount,ct)??0};
        model.InterestPaid=await all.SumAsync(p=>(decimal?)p.interestAmount,ct)??0;
        model.Schedule=await service.ProjectAsync(d,ct);
        model.RateChanges=await db.DebtRateChanges.AsNoTracking().Where(c=>c.debtId==id).OrderByDescending(c=>c.createdDate).Take(50).ToListAsync(ct);
        var query=all;
        if(!string.IsNullOrEmpty(month)){if(!DateTime.TryParseExact(month,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out var start)||start.Year<1000||start.Year>9998)return BadRequest("Choose a valid payment month.");var end=start.AddMonths(1);query=query.Where(p=>p.repaymentDate>=start&&p.repaymentDate<end);}
        if(!string.IsNullOrEmpty(expenseId))query=query.Where(p=>p.expenseId==expenseId);
        model.Count=await query.CountAsync(ct);model.Page=Math.Clamp(page,1,model.Pages);
        model.Repayments=await query.OrderByDescending(p=>p.repaymentDate).ThenByDescending(p=>p.createdDate).ThenByDescending(p=>p.repaymentId).Skip((model.Page-1)*20).Take(20).ToListAsync(ct);
        return Request.Headers["X-Requested-With"]=="XMLHttpRequest"?PartialView("_RepaymentHistory",model):View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Repay(string id,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();var d=await db.Debts.AsNoTracking().SingleOrDefaultAsync(d=>d.debtId==id&&d.userId==Owner&&d.status!="Deleted",ct);if(d==null)return NotFound();
        if(d.status!="Active"||d.outstandingAmount<=0){TempData["WarningMessage"]="Only active debts with an outstanding balance can be repaid.";return RedirectToAction(nameof(Details),new{id});}
        var model=new DebtRepayViewModel{DebtId=id,Revision=d.revision,Debt=d,Amount=Math.Min(d.monthlyRepaymentAmount,d.outstandingAmount),MaximumPayment=d.outstandingAmount};
        if(d.calculationMode=="Calculated"){var period=await service.CurrentPeriodAsync(d,ct);model.EstimatedInterestDue=period.InterestRemaining;model.MaximumPayment=period.MaximumPayment;model.Amount=Math.Min(Math.Max(.01m,period.TargetPayment-period.CashPaid),period.MaximumPayment);}
        return View(model);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Repay(string id,[Bind("DebtId,Revision,Amount,PaymentDate,PaymentMethod,Remark,NextDueDate")]DebtRepayViewModel model,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();if(id!=model.DebtId)return BadRequest();
        model.Debt=await db.Debts.AsNoTracking().SingleOrDefaultAsync(d=>d.debtId==id&&d.userId==Owner&&d.status!="Deleted",ct);if(model.Debt==null)return NotFound();
        if(ModelState.IsValid)try{if(!await service.RepayAsync(Owner,model,ct))return NotFound();TempData["SuccessMessage"]="Repayment recorded, outstanding balance updated, and one Debt Repayment expense created.";return RedirectToAction(nameof(Details),new{id});}catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
        model.MaximumPayment=model.Debt.outstandingAmount;
        if(model.Debt.calculationMode=="Calculated"){var period=await service.CurrentPeriodAsync(model.Debt,ct);model.EstimatedInterestDue=period.InterestRemaining;model.MaximumPayment=period.MaximumPayment;}
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Delete(string id,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();var d=await db.Debts.AsNoTracking().SingleOrDefaultAsync(d=>d.debtId==id&&d.userId==Owner&&d.status!="Deleted",ct);return d==null?NotFound():View(d);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id,int revision,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();try{if(!await service.DeleteAsync(Owner,id,revision,ct))return NotFound();TempData["SuccessMessage"]="Debt removed from the list. Repayment history and expenses are retained.";return RedirectToAction(nameof(Index));}catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;return RedirectToAction(nameof(Delete),new{id});}
    }
    [HttpGet]
    public async Task<IActionResult> Payment(string expenseId,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();var id=await db.DebtRepayments.AsNoTracking().Where(p=>p.expenseId==expenseId&&p.Debt!.userId==Owner).Select(p=>p.debtId).SingleOrDefaultAsync(ct);return id==null?NotFound():RedirectToAction(nameof(Details),new{id,expenseId});
    }

    private void ClearDerivedErrors(DebtFormViewModel m)
    {
        if(m.CalculationMode!="Calculated")return;
        ModelState.Remove(nameof(m.MonthlyRepaymentAmount));
        if(!m.IsExistingLoan)ModelState.Remove(nameof(m.OutstandingAmount));
        if(m.RateType=="Floating")ModelState.Remove(nameof(m.InterestRate));
    }
    [HttpGet]
    public IActionResult Calculator()=>View(new LoanCalculatorViewModel());
    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult Calculator(LoanCalculatorViewModel model)
    {
        if(model.RateType=="Floating")ModelState.Remove(nameof(model.AnnualRate));
        else if(model.RateType=="Fixed"){ModelState.Remove(nameof(model.ReferenceRate));ModelState.Remove(nameof(model.Spread));}
        if(ModelState.IsValid)try
        {
            if(model.RateType!="Fixed"&&model.RateType!="Floating")throw new InvalidOperationException("Choose fixed or floating rate.");
            if(model.RateType=="Floating"&&model.Method=="FlatRate")throw new InvalidOperationException("Flat-rate comparison is for legacy fixed-rate agreements. Choose reducing balance for floating rate.");
            if(model.RateType=="Floating")model.AnnualRate=LoanCalculator.Rate(model.ReferenceRate,model.Spread);
            model.Quote=LoanCalculator.Quote(model.Principal,model.AnnualRate,model.Months,model.Method);
            if(model.Method=="ReducingBalance")model.Schedule=LoanCalculator.Schedule(model.Principal,model.AnnualRate,model.Months).ToList();
        }catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Rate(string id,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();var d=await db.Debts.AsNoTracking().SingleOrDefaultAsync(d=>d.debtId==id&&d.userId==Owner&&d.status!="Deleted",ct);
        if(d==null)return NotFound();
        if(d.calculationMode!="Calculated"||d.rateType!="Floating"||d.status!="Active")return BadRequest("Only active floating-rate loans can change rate.");
        return View(new DebtRateViewModel{DebtId=id,Revision=d.revision,ReferenceRate=d.referenceRate??0,Spread=d.rateSpread??0,Debt=d});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(string id,[Bind("DebtId,Revision,ReferenceRate,Spread")]DebtRateViewModel model,CancellationToken ct=default)
    {
        if(Owner=="")return Challenge();if(id!=model.DebtId)return BadRequest();model.Debt=await db.Debts.AsNoTracking().SingleOrDefaultAsync(d=>d.debtId==id&&d.userId==Owner&&d.status!="Deleted",ct);if(model.Debt==null)return NotFound();
        if(ModelState.IsValid)try{if(!await service.ChangeRateAsync(Owner,model,ct))return NotFound();TempData["SuccessMessage"]="Floating rate updated for the next unpaid instalment. Past repayments remain unchanged.";return RedirectToAction(nameof(Details),new{id});}catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
        return View(model);
    }
}
