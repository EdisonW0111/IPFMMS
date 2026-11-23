using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Controllers;
[Authorize(Roles="User,Parent")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class SharedFinanceController(ApplicationDbContext db,SharedFinanceService service):Controller
{
    private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"";
    private bool Ajax=>Request.Headers["X-Requested-With"]=="XMLHttpRequest";
    [HttpGet]
    public async Task<IActionResult> Index(string? search=null,string type="all",string scope="all",int page=1,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();search=search?.Trim();if(search?.Length>100||!new[]{"all","MonthlyBudget","SavingsGoal"}.Contains(type)||!new[]{"all","owned","joined"}.Contains(scope))return BadRequest("Invalid filters.");
        var query=service.Accessible(Actor);if(!string.IsNullOrEmpty(search))query=query.Where(g=>g.sharedFinanceName.Contains(search));if(type!="all")query=query.Where(g=>g.financeType==type);if(scope=="owned")query=query.Where(g=>db.SharedFinanceMembers.Any(m=>m.sharedFinanceId==g.sharedFinanceId&&m.userId==Actor&&m.invitationStatus=="Accepted"&&m.memberRole=="Owner"));else if(scope=="joined")query=query.Where(g=>!db.SharedFinanceMembers.Any(m=>m.sharedFinanceId==g.sharedFinanceId&&m.userId==Actor&&m.invitationStatus=="Accepted"&&m.memberRole=="Owner"));
        var model=new SharedFinanceIndexViewModel{Owner=Actor,Search=search,Type=type,Scope=scope,Count=await query.CountAsync(ct)};model.Page=Math.Clamp(page,1,model.Pages);
        model.Groups=await query.OrderByDescending(g=>g.createdDate).ThenBy(g=>g.sharedFinanceId).Skip((model.Page-1)*12).Take(12).ToListAsync(ct);var ids=model.Groups.Select(g=>g.sharedFinanceId).ToArray();model.MemberCounts=await db.SharedFinanceMembers.Where(m=>ids.Contains(m.sharedFinanceId)&&m.invitationStatus=="Accepted").GroupBy(m=>m.sharedFinanceId).Select(g=>new{Id=g.Key,Count=g.Count()}).ToDictionaryAsync(g=>g.Id,g=>g.Count,ct);
        model.OwnedGroupIds=new HashSet<string>(await db.SharedFinanceMembers.AsNoTracking().Where(m=>ids.Contains(m.sharedFinanceId)&&m.userId==Actor&&m.invitationStatus=="Accepted"&&m.memberRole=="Owner").Select(m=>m.sharedFinanceId).ToListAsync(ct),StringComparer.OrdinalIgnoreCase);
        var availableIds=service.AvailableGroups().Select(g=>g.sharedFinanceId);
        model.Invitations=await db.SharedFinanceMembers.AsNoTracking().Include(m=>m.Finance)!.ThenInclude(g=>g!.Owner).Where(m=>m.userId==Actor&&m.invitationStatus=="Pending"&&m.Finance!.status=="Active"&&availableIds.Contains(m.sharedFinanceId)&&m.User!.AccountStatus==AccountStatus.Active&&(m.User.Role=="User"||m.User.Role=="Parent")).OrderByDescending(m=>m.invitedDate).Take(20).ToListAsync(ct);
        return Ajax?PartialView("_SharedGroups",model):View(model);
    }
    [HttpGet]public IActionResult Create()=>Actor==""?Challenge():View("Form",new SharedFinanceFormViewModel());
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,FinanceType,Amount,Month,Description,InviteEmails")]SharedFinanceFormViewModel model,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();if(ModelState.IsValid)try{var id=await service.CreateAsync(Actor,model,ct);TempData["SuccessMessage"]="Shared finance created. Invitations are pending until each person accepts; email delivery status is shown with members.";return RedirectToAction(nameof(Details),new{id});}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}return View("Form",model);
    }
    [HttpGet]
    public async Task<IActionResult> Edit(string id,string? month=null,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();var g=await service.Managed(Actor).SingleOrDefaultAsync(g=>g.sharedFinanceId==id,ct);if(g==null)return NotFound();
        var model=new SharedFinanceFormViewModel{Id=id,Revision=g.revision,Name=g.sharedFinanceName,FinanceType=g.financeType,Description=g.description,Status=g.status,Month=month??SharedFinanceService.Today.ToString("yyyy-MM")};
        if(g.financeType=="SavingsGoal")model.Amount=await db.SharedSavingsGoals.Where(s=>s.sharedFinanceId==id).Select(s=>(decimal?)s.targetAmount).SingleAsync(ct);
        else{DateTime date;try{date=SharedFinanceService.Month(model.Month);}catch(InvalidOperationException ex){return BadRequest(ex.Message);}model.Amount=await db.SharedBudgets.Where(b=>b.sharedFinanceId==id&&b.budgetMonth==date).Select(b=>(decimal?)b.budgetAmount).SingleOrDefaultAsync(ct);}
        return View("Form",model);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id,[Bind("Id,Revision,Name,FinanceType,Amount,Month,Description,Status")]SharedFinanceFormViewModel model,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();if(id!=model.Id)return BadRequest();if(!await service.Managed(Actor).AnyAsync(g=>g.sharedFinanceId==id,ct))return NotFound();
        if(ModelState.IsValid)try{await service.EditAsync(Actor,model,ct);TempData["SuccessMessage"]="Shared finance updated.";return RedirectToAction(nameof(Details),new{id,month=model.Month});}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}return View("Form",model);
    }
    [HttpGet]
    public async Task<IActionResult> Details(string id,string? month=null,string type="all",int page=1,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();var g=await service.Accessible(Actor).SingleOrDefaultAsync(g=>g.sharedFinanceId==id,ct);if(g==null)return NotFound();DateTime date;try{date=SharedFinanceService.Month(month??SharedFinanceService.Today.ToString("yyyy-MM"));}catch(InvalidOperationException ex){return BadRequest(ex.Message);}
        if(!new[]{"all","Expense","Contribution","Withdrawal"}.Contains(type))return BadRequest("Invalid transaction type.");
        var model=new SharedFinanceDetailsViewModel{Finance=g,Owner=Actor,Month=date.ToString("yyyy-MM"),TransactionType=type,IsOwner=await service.Managed(Actor).AnyAsync(x=>x.sharedFinanceId==id,ct),ActiveOwnerCount=await service.ActiveOwnerCountAsync(id,ct)};var all=db.SharedFinanceTransactions.AsNoTracking().Where(t=>t.sharedFinanceId==id);var end=date.AddMonths(1);
        model.Saved=await service.SavedAsync(id,null,ct);model.MySaved=await service.SavedAsync(id,Actor,ct);
        if(g.financeType=="MonthlyBudget"){model.BudgetAmount=await db.SharedBudgets.Where(b=>b.sharedFinanceId==id&&b.budgetMonth==date).Select(b=>(decimal?)b.budgetAmount).SingleOrDefaultAsync(ct)??0;model.Spent=await all.Where(t=>t.transactionType=="Expense"&&t.transactionDate>=date&&t.transactionDate<end).SumAsync(t=>(decimal?)t.amount,ct)??0;}
        else model.TargetAmount=await db.SharedSavingsGoals.Where(s=>s.sharedFinanceId==id).Select(s=>s.targetAmount).SingleAsync(ct);
        var query=all.Where(t=>t.transactionDate>=date&&t.transactionDate<end);if(type!="all")query=query.Where(t=>t.transactionType==type);model.Count=await query.CountAsync(ct);model.Page=Math.Clamp(page,1,model.Pages);model.Transactions=await query.Include(t=>t.User).Include(t=>t.ExpenseCategory).OrderByDescending(t=>t.transactionDate).ThenByDescending(t=>t.createdDate).ThenBy(t=>t.sharedTransactionId).Skip((model.Page-1)*20).Take(20).ToListAsync(ct);
        // Accepted Owners can see pending/rejected membership state and delivery information.
        model.Members=await db.SharedFinanceMembers.AsNoTracking().Include(m=>m.User).Where(m=>m.sharedFinanceId==id&&(model.IsOwner||m.invitationStatus=="Accepted")).OrderBy(m=>m.memberRole=="Owner"?0:1).ThenBy(m=>m.invitedDate).ToListAsync(ct);
        model.ExpectedShares = SharedShareCalculator.Calculate(g.financeType == "MonthlyBudget" ? model.BudgetAmount : model.TargetAmount, model.Members);
        model.ActualShares = await all.Where(t => g.financeType == "SavingsGoal" || (t.transactionDate >= date && t.transactionDate < end))
            .GroupBy(t => t.userId).Select(g => new { Id = g.Key, Amount = g.Sum(t => t.transactionType == "Withdrawal" ? -t.amount : t.amount) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        return Ajax?PartialView("_SharedDetailsResults",model):View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Shares(string id, CancellationToken ct = default)
    {
        if (Actor == "") return Challenge();
        var group = await service.Managed(Actor).SingleOrDefaultAsync(g => g.sharedFinanceId == id, ct);
        if (group == null) return NotFound();
        var members = await db.SharedFinanceMembers.AsNoTracking().Include(m => m.User).Where(m => m.sharedFinanceId == id && m.invitationStatus == "Accepted").OrderBy(m => m.userId).ToListAsync(ct);
        return View(new SharedSharesViewModel { Id = id, Revision = group.revision, GroupName = group.sharedFinanceName,
            Members = members.Select(m => new SharedShareRow { UserId = m.userId, Name = m.User?.FullName ?? m.userId, Percentage = m.sharePercentage }).ToList() });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Shares(SharedSharesViewModel model, CancellationToken ct = default)
    {
        if (Actor == "") return Challenge();
        var group = await service.Managed(Actor).SingleOrDefaultAsync(g => g.sharedFinanceId == model.Id, ct);
        if (group == null) return NotFound();
        model.GroupName = group.sharedFinanceName;
        var names = await db.SharedFinanceMembers.AsNoTracking().Include(m => m.User).Where(m => m.sharedFinanceId == model.Id && m.invitationStatus == "Accepted").ToDictionaryAsync(m => m.userId, m => m.User!.FullName, ct);
        foreach (var row in model.Members) row.Name = names.GetValueOrDefault(row.UserId) ?? row.UserId;
        if (ModelState.IsValid) try {
            await service.SetSharesAsync(Actor, model, ct);
            TempData["SuccessMessage"] = "Expected shares saved. Actual payments and personal finances are unchanged.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        } catch (SharedAccessException) { return NotFound(); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Invite(string id,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();var g=await service.Managed(Actor).SingleOrDefaultAsync(g=>g.sharedFinanceId==id,ct);return g==null?NotFound():View(new SharedInvitationViewModel{Id=id,Finance=g});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(string id,[Bind("Id,Email")]SharedInvitationViewModel model,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();if(id!=model.Id)return BadRequest();model.Finance=await service.Managed(Actor).SingleOrDefaultAsync(g=>g.sharedFinanceId==id,ct);if(model.Finance==null)return NotFound();
        if(ModelState.IsValid)try{await service.InviteAsync(Actor,id,model.Email,ct);TempData["SuccessMessage"]="Invitation saved. The recipient must accept before viewing group finances.";return RedirectToAction(nameof(Details),new{id});}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}return View(model);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Decision(string id,int version,bool accept,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();try{await service.DecideAsync(Actor,id,version,accept,ct);TempData["SuccessMessage"]=accept?"Invitation accepted.":"Invitation rejected.";return accept?RedirectToAction(nameof(Details),new{id}):RedirectToAction(nameof(Index));}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;return RedirectToAction(nameof(Index));}
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> MemberAction(string id,string userId,int version,string action,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();try{await service.MemberActionAsync(Actor,id,userId,version,action,ct);TempData["SuccessMessage"]="Membership updated.";}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;}return RedirectToAction(nameof(Details),new{id});
    }
    [HttpGet]
    public async Task<IActionResult> PromoteOwner(string id,string userId,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();
        var group=await service.Managed(Actor).SingleOrDefaultAsync(g=>g.sharedFinanceId==id&&g.status=="Active",ct);
        if(group==null)return NotFound();
        var member=await db.SharedFinanceMembers.AsNoTracking().Include(m=>m.User).SingleOrDefaultAsync(m=>m.sharedFinanceId==id&&m.userId==userId
            &&m.memberRole=="Member"&&m.invitationStatus=="Accepted"&&m.User!.AccountStatus==AccountStatus.Active&&(m.User.Role=="User"||m.User.Role=="Parent"),ct);
        if(member==null)return NotFound();
        return View(new PromoteSharedOwnerViewModel{Id=group.sharedFinanceId,UserId=member.userId,MemberName=member.User!.FullName,
            GroupName=group.sharedFinanceName,Revision=group.revision,InvitationVersion=member.invitationVersion});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteOwner(string id,[Bind("Id,UserId,Revision,InvitationVersion")]PromoteSharedOwnerViewModel model,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();
        if(id!=model.Id||!ModelState.IsValid)return BadRequest("Invalid promotion confirmation.");
        try{await service.PromoteOwnerAsync(Actor,id,model.UserId,model.Revision,model.InvitationVersion,ct);TempData["SuccessMessage"]="Member promoted to Owner. Existing Owners keep their management access.";}
        catch(SharedAccessException){return NotFound();}
        catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;}
        return RedirectToAction(nameof(Details),new{id});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(string id,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();try{await service.LeaveAsync(Actor,id,ct);TempData["SuccessMessage"]="You left the arrangement.";return RedirectToAction(nameof(Index));}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;return RedirectToAction(nameof(Details),new{id});}
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(string id,string userId,int version,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();try{await service.DeliverInviteAsync(Actor,id,userId,version,ct);TempData["SuccessMessage"]="Email delivery was checked. See the member's delivery status; failed emails can be retried after one minute.";}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;}return RedirectToAction(nameof(Details),new{id});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Budget(string id,int revision,string month,decimal amount,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();if(!ModelState.IsValid)return BadRequest("Invalid budget.");try{await service.BudgetAsync(Actor,id,revision,month,amount,ct);TempData["SuccessMessage"]="Monthly shared budget saved.";}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;}return RedirectToAction(nameof(Details),new{id,month});
    }
    private async Task Populate(SharedEntryViewModel model,CancellationToken ct)=>model.Categories=await db.ExpenseCategories.AsNoTracking().Where(c=>c.IsActive).OrderBy(c=>c.DisplayOrder).ThenBy(c=>c.CategoryName).Select(c=>new SelectListItem(c.CategoryName,c.ExpenseCategoryId)).ToListAsync(ct);
    [HttpGet]
    public async Task<IActionResult> Entry(string id,string? type=null,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();var g=await service.Accessible(Actor).SingleOrDefaultAsync(g=>g.sharedFinanceId==id,ct);if(g==null)return NotFound();var model=new SharedEntryViewModel{Id=id,Finance=g,Type=type??(g.financeType=="MonthlyBudget"?"Expense":"Contribution")};await Populate(model,ct);return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Release(string id,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();if(await service.SavedAsync(id,Actor,ct)<=0)return NotFound();var g=await db.SharedFinances.AsNoTracking().SingleOrDefaultAsync(g=>g.sharedFinanceId==id&&g.financeType=="SavingsGoal",ct);if(g==null)return NotFound();var model=new SharedEntryViewModel{Id=id,Finance=g,Type="Withdrawal"};await Populate(model,ct);return View("Entry",model);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Entry(string id,[Bind("Id,SubmissionKey,Type,Amount,Date,CategoryId,PaymentMethod,Description")]SharedEntryViewModel model,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();if(id!=model.Id)return BadRequest();
        model.Finance=await service.Accessible(Actor).SingleOrDefaultAsync(g=>g.sharedFinanceId==id,ct);if(model.Finance==null&&model.Type=="Withdrawal"&&await service.SavedAsync(id,Actor,ct)>0)model.Finance=await db.SharedFinances.AsNoTracking().SingleOrDefaultAsync(g=>g.sharedFinanceId==id&&g.financeType=="SavingsGoal",ct);if(model.Finance==null)return NotFound();
        if(ModelState.IsValid)try{await service.RecordAsync(Actor,model,ct);TempData["SuccessMessage"]=model.Type=="Expense"?"Shared spending recorded with one linked personal expense.":model.Type=="Contribution"?"Shared savings allocated from your available balance.":"Your shared savings allocation was released back to available funds.";return await service.Accessible(Actor).AnyAsync(g=>g.sharedFinanceId==id,ct)?RedirectToAction(nameof(Details),new{id,month=model.Date?.ToString("yyyy-MM")}):RedirectToAction(nameof(MyEntries));}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}await Populate(model,ct);return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Delete(string id,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();var g=await service.Managed(Actor).SingleOrDefaultAsync(g=>g.sharedFinanceId==id,ct);return g==null?NotFound():View(g);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id,int revision,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();try{await service.DeleteAsync(Actor,id,revision,ct);TempData["SuccessMessage"]="Arrangement removed. Financial entries remain in each member's personal records; own savings can be released from My shared entries.";return RedirectToAction(nameof(Index));}catch(SharedAccessException){return NotFound();}catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;return RedirectToAction(nameof(Delete),new{id});}
    }
    [HttpGet]
    public async Task<IActionResult> MyEntries(int page=1,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();var query=db.SharedFinanceTransactions.AsNoTracking().Where(t=>t.userId==Actor);var model=new SharedPersonalViewModel{Count=await query.CountAsync(ct)};model.Page=Math.Clamp(page,1,model.Pages);model.Entries=await query.Include(t=>t.Finance).Include(t=>t.ExpenseCategory).OrderByDescending(t=>t.transactionDate).ThenByDescending(t=>t.createdDate).Skip((model.Page-1)*20).Take(20).ToListAsync(ct);
        model.Balances=await query.Where(t=>t.transactionType=="Contribution"||t.transactionType=="Withdrawal").GroupBy(t=>t.sharedFinanceId).Select(g=>new{Id=g.Key,Amount=g.Sum(t=>t.transactionType=="Withdrawal"?-t.amount:t.amount)}).Where(x=>x.Amount>0).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);var ids=model.Balances.Keys.ToArray();model.SavingsGroups=await db.SharedFinances.AsNoTracking().Where(g=>ids.Contains(g.sharedFinanceId)).ToListAsync(ct);return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> PersonalEntry(string expenseId,CancellationToken ct=default)
    {
        if(Actor=="")return Challenge();var entry=await db.SharedFinanceTransactions.AsNoTracking().Include(t=>t.Finance).Include(t=>t.ExpenseCategory).SingleOrDefaultAsync(t=>t.expenseId==expenseId&&t.userId==Actor,ct);return entry==null?NotFound():View(entry);
    }
}
