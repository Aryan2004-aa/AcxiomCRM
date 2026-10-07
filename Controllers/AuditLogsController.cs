using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = Roles.Admin)]   // read-only: there are intentionally no edit/delete actions
public class AuditLogsController : BaseController
{
    public AuditLogsController(ApplicationDbContext db, IAuditService audit) : base(db, audit) { }

    public async Task<IActionResult> Index(string user, string module, string action, DateTime? from, DateTime? to, int page = 1)
    {
        var q = Db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(user)) { user = user.Trim().ToLower(); q = q.Where(a => (a.UserName ?? "").ToLower().Contains(user)); }
        if (!string.IsNullOrEmpty(module)) q = q.Where(a => a.EntityName == module);
        if (!string.IsNullOrEmpty(action)) q = q.Where(a => a.Action == action);
        if (from != null) q = q.Where(a => a.CreatedDate >= from.Value.Date);
        if (to != null) q = q.Where(a => a.CreatedDate < to.Value.Date.AddDays(1));
        ViewBag.User = user; ViewBag.Module = module; ViewBag.ActionFilter = action; ViewBag.From = from; ViewBag.To = to;
        ViewBag.Modules = new SelectList(await Db.AuditLogs.Select(a => a.EntityName).Distinct().OrderBy(x => x).ToListAsync());
        ViewBag.Actions = new SelectList(await Db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(x => x).ToListAsync());
        return View(await PagedList<AuditLog>.CreateAsync(q.OrderByDescending(a => a.AuditLogId), page, 20));
    }
}
