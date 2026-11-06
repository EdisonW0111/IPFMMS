using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
namespace IPFMMS.Controllers;
[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ParentStudentLinksController(ApplicationDbContext db, ParentStudentLinkService links) : Controller
{
    private IQueryable<ParentStudentLinkRow> Rows() => db.ParentStudentLinks.AsNoTracking().Select(l => new ParentStudentLinkRow
    {
        LinkId = l.linkId, ParentName = l.Parent!.FullName, ParentEmail = l.Parent.Email,
        StudentName = l.Student!.FullName, StudentEmail = l.Student.Email, Status = l.linkStatus, LinkedDate = l.linkedDate
    });

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, string status = "All", int page = 1, CancellationToken ct = default)
    {
        if (status != "All" && status != "Active" && status != "Inactive") status = "All";
        search = (search ?? "").Trim();
        if (search.Length > 100) search = search[..100];
        var query = Rows();
        if (search.Length > 0) query = query.Where(l => l.ParentName.Contains(search) || l.ParentEmail.Contains(search) || l.StudentName.Contains(search) || l.StudentEmail.Contains(search));
        if (status != "All") query = query.Where(l => l.Status == status);
        var model = new ParentStudentLinkIndexViewModel { Search = search, Status = status, Count = await query.CountAsync(ct) };
        model.Page = Math.Clamp(page, 1, model.Pages);
        model.Links = await query.OrderByDescending(l => l.LinkedDate).ThenBy(l => l.LinkId).Skip((model.Page - 1) * 20).Take(20).ToListAsync(ct);
        return View(model);
    }

    private async Task PopulateAsync(CreateParentStudentLinkViewModel model, CancellationToken ct)
    {
        var accounts = await db.Users.AsNoTracking().Where(u => u.AccountStatus == AccountStatus.Active && (u.Role == "Parent" || u.Role == "User"))
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId).Select(u => new { u.UserId, u.FullName, u.Email, u.Role }).ToListAsync(ct);
        model.Parents = accounts.Select(u => new SelectListItem(u.FullName + " (" + u.Email + ")", u.UserId)).ToList();
        model.Students = accounts.Select(u => new SelectListItem(u.FullName + " (" + u.Email + ")", u.UserId)).ToList();
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        var model = new CreateParentStudentLinkViewModel(); await PopulateAsync(model, ct); return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ParentId,UserId")] CreateParentStudentLinkViewModel model, CancellationToken ct = default)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await links.CreateAsync(model.ParentId, model.UserId, ct);
                TempData["SuccessMessage"] = "Parent and student accounts linked.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
            { ModelState.AddModelError(string.Empty, "This relationship already exists. Check the relationship list."); }
        }
        await PopulateAsync(model, ct); return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Manage(string id, CancellationToken ct = default)
    {
        var model = await Rows().SingleOrDefaultAsync(l => l.LinkId == id, ct);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool active, CancellationToken ct = default)
    {
        try
        {
            if (!await links.SetActiveAsync(id, active, ct)) return NotFound();
            TempData["SuccessMessage"] = active ? "Relationship reactivated." : "Relationship deactivated. The record has been retained.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            var model = await Rows().SingleOrDefaultAsync(l => l.LinkId == id, ct);
            if (model == null) return NotFound();
            ModelState.AddModelError(string.Empty, ex.Message); return View("Manage", model);
        }
    }
}
