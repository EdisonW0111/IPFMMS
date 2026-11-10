using System.Security.Claims;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace IPFMMS.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminUsersController(AdminUserService users) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, string status = "All", string role = "All", int page = 1, CancellationToken ct = default)
    {
        try { var model = await users.ListAsync(Actor, search, status, role, page, ct);
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? PartialView("_UserResults", model) : View(model); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken ct = default)
    {
        try { var model = await users.DetailsAsync(Actor, id, ct); return model == null ? NotFound() : View(model); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken ct = default)
    {
        try { var model = await users.EditModelAsync(Actor, id, ct); return model == null ? NotFound() : View(model); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("UserId,FullName,PhoneNumber,Role,AccountStatus,EditStamp")] AdminUserEditViewModel model, CancellationToken ct = default)
    {
        if (id != model.UserId) return BadRequest();
        try {
            var current = await users.EditModelAsync(Actor, id, ct); if (current == null) return NotFound();
            if (ModelState.IsValid) try {
                if (!await users.EditAsync(Actor, id, model, ct)) return NotFound();
                TempData["SuccessMessage"] = "Account changes saved."; return RedirectToAction(nameof(Details), new { id });
            } catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            model.Email = current.Email; model.IsSelf = current.IsSelf; model.IsPending = current.IsPending; model.IsDeleted = current.IsDeleted;
            return View(model);
        } catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        try { var current = await users.DetailsAsync(Actor, id, ct); if (current == null) return NotFound();
            if (!current.CanDelete) return BadRequest("This account cannot be deleted.");
            return View(new AdminUserDeleteViewModel { User = current.User, EditStamp = current.EditStamp }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, [Bind("EditStamp")] AdminUserDeleteViewModel model, CancellationToken ct = default)
    {
        try {
            var current = await users.DetailsAsync(Actor, id, ct); if (current == null) return NotFound();
            if (ModelState.IsValid) try {
                if (!await users.DeleteAsync(Actor, id, model.EditStamp, ct)) return NotFound();
                TempData["SuccessMessage"] = "Account deleted from active use. Sign-in is disabled and financial history is retained.";
                return RedirectToAction(nameof(Index));
            } catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            model.User = current.User; return View(model);
        } catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
