using System.Security.Claims;
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
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private async Task<IActionResult?> DeniedAsync(CancellationToken ct)
    {
        if (Actor == "") return Challenge();
        try { await AdminGuard.RequireAsync(db, Actor, ct); return null; }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    private IQueryable<ParentStudentLink> Rows() =>
        db.ParentStudentLinks.AsNoTracking().Include(l => l.Parent).Include(l => l.Student);
    private static ParentStudentLinkRow Row(ParentStudentLink link) => new()
    {
        LinkId = link.linkId, ParentId = link.parentId, UserId = link.userId,
        ParentName = link.Parent?.FullName ?? link.parentId, ParentEmail = link.Parent?.Email ?? "",
        StudentName = link.Student?.FullName ?? link.userId, StudentEmail = link.Student?.Email ?? "",
        Status = link.linkStatus, LinkedDate = link.linkedDate, RequestedDate = link.requestedDate,
        AcceptedDate = link.acceptedDate, ParentHiddenDate = link.parentHiddenDate,
        StudentHiddenDate = link.studentHiddenDate, EmailSentDate = link.emailSentDate,
        Version = link.requestVersion, Stamp = ParentStudentLinkService.Stamp(link),
        AccountsEligible = link.Parent is { AccountStatus: AccountStatus.Active } && link.Student is { AccountStatus: AccountStatus.Active }
            && (link.Parent.Role == "User" || link.Parent.Role == "Parent")
            && (link.Student.Role == "User" || link.Student.Role == "Parent")
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, string status = "All", int page = 1, CancellationToken ct = default)
    {
        if (await DeniedAsync(ct) is IActionResult denied) return denied;
        if (status != "All" && status != "Pending" && status != "Active" && status != "Inactive"
            && status != "Rejected" && status != "Cancelled") status = "All";
        search = (search ?? "").Trim();
        if (search.Length > 100) search = search[..100];
        var query = Rows();
        if (search.Length > 0) query = query.Where(l => l.Parent!.FullName.Contains(search) || l.Parent.Email.Contains(search)
            || l.Student!.FullName.Contains(search) || l.Student.Email.Contains(search)
            || l.linkId.Contains(search) || l.parentId.Contains(search) || l.userId.Contains(search));
        if (status != "All") query = query.Where(l => l.linkStatus == status);
        var model = new ParentStudentLinkIndexViewModel { Search = search, Status = status, Count = await query.CountAsync(ct) };
        model.Page = Math.Clamp(page, 1, model.Pages);
        var rows = await query.OrderByDescending(l => l.requestedDate ?? l.linkedDate).ThenBy(l => l.linkId)
            .Skip((model.Page - 1) * 20).Take(20).ToListAsync(ct);
        model.Links = rows.Select(Row).ToList();
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
            ? PartialView("_RelationshipResults", model) : View(model);
    }

    private async Task PopulateAsync(CreateParentStudentLinkViewModel model, CancellationToken ct)
    {
        var accounts = await db.Users.AsNoTracking().Where(u => u.AccountStatus == AccountStatus.Active && (u.Role == "Parent" || u.Role == "User"))
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId).Select(u => new { u.UserId, u.FullName, u.Email }).ToListAsync(ct);
        model.Parents = accounts.Select(u => new SelectListItem(u.FullName + " (" + u.Email + ")", u.UserId)).ToList();
        model.Students = accounts.Select(u => new SelectListItem(u.FullName + " (" + u.Email + ")", u.UserId)).ToList();
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        if (await DeniedAsync(ct) is IActionResult denied) return denied;
        var model = new CreateParentStudentLinkViewModel(); await PopulateAsync(model, ct); return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ParentId,UserId")] CreateParentStudentLinkViewModel model, CancellationToken ct = default)
    {
        if (await DeniedAsync(ct) is IActionResult denied) return denied;
        if (ModelState.IsValid)
        {
            try
            {
                var result = await links.CreateAsync(Actor, model.ParentId, model.UserId, ct);
                TempData[result.Delivery == InvitationEmailDelivery.Failed ? "WarningMessage" : "SuccessMessage"] = result.Delivery switch
                {
                    InvitationEmailDelivery.Sent => "Pending request created and email sent. Monitoring starts only after the recipient accepts.",
                    InvitationEmailDelivery.Disabled => "Pending request created. The recipient has turned off invitation emails and can review it in Parental Monitoring.",
                    InvitationEmailDelivery.Suppressed => "Request created. Its email was not sent because the request is no longer available for delivery.",
                    _ => "Pending request created, but its email could not be sent. It remains available for the recipient to review."
                };
                return RedirectToAction(nameof(Manage), new { id = result.LinkId });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
            { ModelState.AddModelError(string.Empty, "This relationship changed. Review the existing request in the relationship list."); }
        }
        await PopulateAsync(model, ct); return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Manage(string id, CancellationToken ct = default)
    {
        if (await DeniedAsync(ct) is IActionResult denied) return denied;
        var link = await Rows().SingleOrDefaultAsync(l => l.linkId == id, ct);
        return link == null ? NotFound() : View(Row(link));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(string id, int version, string? stamp, CancellationToken ct = default) =>
        ChangeAsync(id, version, stamp, cancel: true, ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate(string id, int version, string? stamp, CancellationToken ct = default) =>
        ChangeAsync(id, version, stamp, cancel: false, ct);

    private async Task<IActionResult> ChangeAsync(string id, int version, string? stamp, bool cancel, CancellationToken ct)
    {
        if (await DeniedAsync(ct) is IActionResult denied) return denied;
        try
        {
            var changed = cancel ? await links.CancelAsync(Actor, id, version, stamp, ct)
                : await links.DeactivateAsync(Actor, id, version, stamp, ct);
            if (!changed) return NotFound();
            TempData["SuccessMessage"] = cancel
                ? "Pending request cancelled. No monitoring access was granted."
                : "Monitoring access deactivated. History is retained; a new request requires the recipient's acceptance.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["WarningMessage"] = ex.Message; }
        return RedirectToAction(nameof(Manage), new { id });
    }

    // Retain the old route safely: it can never grant or reactivate monitoring access.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool active, int version, string? stamp, CancellationToken ct = default)
    {
        if (await DeniedAsync(ct) is IActionResult denied) return denied;
        if (active) return BadRequest("Administrators cannot grant consent. Create a pending request for the recipient to accept.");
        return await ChangeAsync(id, version, stamp, cancel: false, ct);
    }
}
