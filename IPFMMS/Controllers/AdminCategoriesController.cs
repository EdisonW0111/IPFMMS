using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IPFMMS.Controllers;

[Authorize(Roles="Admin")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AdminCategoriesController(ApplicationDbContext db,AdminCategoryService categories):Controller
{
    private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"";
    private async Task<bool> Authorized(CancellationToken ct)
    {if(Actor=="")return false;try{await AdminGuard.RequireAsync(db,Actor,ct);return true;}catch(UnauthorizedAccessException){return false;}}
    [HttpGet]
    public async Task<IActionResult> Index(string kind="Income",string? search=null,string status="All",int page=1,CancellationToken ct=default)
    {
        if(!await Authorized(ct))return Forbid();
        try{var model=await categories.ListAsync(Actor,kind,search,status,page,ct);return Request.Headers["X-Requested-With"]=="XMLHttpRequest"?PartialView("_CategoryResults",model):View(model);}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
        catch(InvalidOperationException ex){return BadRequest(ex.Message);}
    }
    [HttpGet]
    public async Task<IActionResult> Create(string kind="Income",CancellationToken ct=default)
    {
        if(!await Authorized(ct))return Forbid();
        try{return View(new AdminCategoryFormViewModel{Kind=AdminCategoryService.ValidateKind(kind)});}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string kind,[Bind("CategoryName,Description,DisplayOrder,IsActive")]AdminCategoryFormViewModel model,CancellationToken ct=default)
    {
        if(!await Authorized(ct))return Forbid();
        try{model.Kind=AdminCategoryService.ValidateKind(kind);if(ModelState.IsValid){await categories.CreateAsync(Actor,kind,model,ct);TempData["SuccessMessage"]="Category created.";return RedirectToAction(nameof(Index),new{kind});}}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
        catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Edit(string kind,string id,CancellationToken ct=default)
    {
        if(!await Authorized(ct))return Forbid();
        try{var model=await categories.GetFormAsync(Actor,kind,id,ct);return model==null?NotFound():View(model);}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string kind,string id,[Bind("CategoryId,EditStamp,CategoryName,Description,DisplayOrder,IsActive")]AdminCategoryFormViewModel model,CancellationToken ct=default)
    {
        if(!await Authorized(ct))return Forbid();if(id!=model.CategoryId)return BadRequest();
        try{
            model.Kind=AdminCategoryService.ValidateKind(kind);var current=await categories.GetFormAsync(Actor,kind,id,ct);if(current==null)return NotFound();model.IsProtectedName=current.IsProtectedName;
            if(ModelState.IsValid){if(!await categories.EditAsync(Actor,kind,id,model,ct))return NotFound();TempData["SuccessMessage"]="Category updated. Existing financial records are unchanged.";return RedirectToAction(nameof(Index),new{kind});}
        }
        catch(UnauthorizedAccessException){return Forbid();}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
        catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Delete(string kind,string id,CancellationToken ct=default)
    {
        if(!await Authorized(ct))return Forbid();
        try{var model=await categories.GetDeleteAsync(Actor,kind,id,ct);return model==null?NotFound():View(model);}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
    }
    [HttpPost,ActionName("Delete"),ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string kind,string id,[Bind("CategoryId,EditStamp")]AdminCategoryDeleteViewModel model,CancellationToken ct=default)
    {
        if(!await Authorized(ct))return Forbid();if(id!=model.CategoryId)return BadRequest();
        try{if(!await categories.DeleteAsync(Actor,kind,id,model.EditStamp,ct))return NotFound();TempData["SuccessMessage"]="Unused category deleted.";return RedirectToAction(nameof(Index),new{kind});}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
        catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;return RedirectToAction(nameof(Delete),new{kind,id});}
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string kind,string id,string stamp,CancellationToken ct=default)
    {
        if(!await Authorized(ct))return Forbid();
        try{if(!await categories.DeactivateAsync(Actor,kind,id,stamp,ct))return NotFound();TempData["SuccessMessage"]="Category deactivated. Existing references are retained.";return RedirectToAction(nameof(Index),new{kind});}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
        catch(InvalidOperationException ex){TempData["WarningMessage"]=ex.Message;return RedirectToAction(nameof(Delete),new{kind,id});}
    }
}
