using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class LeadsController : BaseController
{
    private readonly LeadService _svc;
    public LeadsController(ApplicationDbContext db, IAuditService audit, LeadService svc) : base(db, audit) { _svc = svc; }

    public async Task<IActionResult> Index(string q, LeadStatus? status, string assignedTo, int page = 1)
    {
        var query = Db.Leads.Include(l => l.AssignedUser).ForUser(UserId, Privileged);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim().ToLower();
            query = query.Where(l => l.LeadName.ToLower().Contains(q) || (l.CompanyName ?? "").ToLower().Contains(q));
        }
        if (status != null) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrEmpty(assignedTo)) query = query.Where(l => l.AssignedTo == assignedTo);
        ViewBag.Q = q; ViewBag.Status = status;
        await FillAllUsersForFilter(assignedTo);
        return View(await PagedList<Lead>.CreateAsync(query.OrderByDescending(l => l.LeadId), page, PageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var l = await Db.Leads.Include(x => x.AssignedUser).FirstOrDefaultAsync(x => x.LeadId == id);
        if (l == null) return NotFound();
        if (!l.CanAccess(UserId, Privileged)) return Forbid();
        ViewBag.FollowUps = await Db.FollowUps.Where(f => f.LeadId == id).OrderByDescending(f => f.FollowUpDate).ToListAsync();
        ViewBag.Activities = await Db.Activities.Where(a => a.LeadId == id).OrderByDescending(a => a.ActivityDate).ToListAsync();
        return View(l);
    }

    public async Task<IActionResult> Create()
    {
        await FillUsers(UserId);
        return View(new Lead { AssignedTo = UserId });
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("LeadName,Email,Phone,CompanyName,Source,Priority,ExpectedValue,Notes,AssignedTo")] Lead l)
    {
        if (!Privileged) { l.AssignedTo = UserId; ModelState.Remove(nameof(Lead.AssignedTo)); }
        LeadService.Normalize(l);
        if (ModelState.IsValid) await ValidateAssignee(l.AssignedTo);
        if (!ModelState.IsValid) { await FillUsers(l.AssignedTo); return View(l); }

        await _svc.CreateAsync(l);
        await Audit.LogAsync("Create", "Lead", l.LeadId.ToString(), newValue: AuditService.Snapshot(l));
        TempData["Success"] = "Lead created successfully.";
        return RedirectToAction(nameof(Details), new { id = l.LeadId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var l = await Db.Leads.FindAsync(id);
        if (l == null) return NotFound();
        if (!l.CanAccess(UserId, Privileged)) return Forbid();
        await FillUsers(l.AssignedTo);
        ViewBag.StatusOptions = LeadWorkflow.Options(l.Status);
        return View(l);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string assignedTo)
    {
        var l = await Db.Leads.FindAsync(id);
        if (l == null) return NotFound();
        if (!l.CanAccess(UserId, Privileged)) return Forbid();
        var old = AuditService.Snapshot(l);
        var oldStatus = l.Status;

        var ok = await TryUpdateModelAsync(l, "", x => x.LeadName, x => x.Email, x => x.Phone, x => x.CompanyName, x => x.Source,
                                           x => x.Priority, x => x.ExpectedValue, x => x.Notes, x => x.Status);
        LeadService.Normalize(l);
        // Workflow: only configured transitions; "Converted" only via the Convert action.
        if (ok && (!LeadWorkflow.IsAllowed(oldStatus, l.Status) || (l.Status == LeadStatus.Converted && oldStatus != LeadStatus.Converted)))
        {
            ModelState.AddModelError(nameof(Lead.Status), $"Invalid status change: {oldStatus} → {l.Status}.");
            ok = false;
        }
        if (ok && Privileged && !string.IsNullOrEmpty(assignedTo) && assignedTo != l.AssignedTo)
        {
            if (await ValidateAssignee(assignedTo)) l.AssignedTo = assignedTo; else ok = false;
        }
        if (!ok)
        {
            await FillUsers(l.AssignedTo);
            ViewBag.StatusOptions = LeadWorkflow.Options(oldStatus);
            return View(l);
        }
        await Db.SaveChangesAsync();
        await Audit.LogAsync(oldStatus != l.Status ? "StatusChange" : "Update", "Lead", id.ToString(), old, AuditService.Snapshot(l));
        TempData["Success"] = "Lead updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Convert(int id)
    {
        var l = await Db.Leads.FindAsync(id);
        if (l == null) return NotFound();
        if (!l.CanAccess(UserId, Privileged)) return Forbid();
        if (l.Status != LeadStatus.Qualified)
        {
            TempData["Error"] = "Only Qualified leads can be converted.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var old = AuditService.Snapshot(l);
        var (customer, opp) = await _svc.ConvertAsync(l, UserId);
        await Audit.LogAsync("LeadConversion", "Lead", id.ToString(), old, AuditService.Snapshot(l));
        await Audit.LogAsync("Create", "Customer", customer.CustomerId.ToString(), newValue: AuditService.Snapshot(customer));
        if (opp != null) await Audit.LogAsync("Create", "Opportunity", opp.OpportunityId.ToString(), newValue: AuditService.Snapshot(opp));
        TempData["Success"] = "Lead converted to customer" + (opp != null ? " and opportunity." : ".");
        return RedirectToAction("Details", "Customers", new { id = customer.CustomerId });
    }

    [HttpPost, Authorize(Roles = Roles.Privileged)]
    public async Task<IActionResult> Delete(int id)
    {
        var l = await Db.Leads.FindAsync(id);
        if (l == null) return NotFound();
        var old = AuditService.Snapshot(l);
        Db.Leads.Remove(l);
        await Db.SaveChangesAsync();
        await Audit.LogAsync("Delete", "Lead", id.ToString(), old);
        TempData["Success"] = "Lead deleted.";
        return RedirectToAction(nameof(Index));
    }
}
