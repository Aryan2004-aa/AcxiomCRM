using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class ActivitiesController : BaseController
{
    public ActivitiesController(ApplicationDbContext db, IAuditService audit) : base(db, audit) { }

    public async Task<IActionResult> Index(ActivityType? type, ActivityStatus? status, DateTime? from, DateTime? to, string assignedTo, int page = 1)
    {
        var query = Db.Activities.Include(a => a.Customer).Include(a => a.Lead).Include(a => a.AssignedUser).ForUser(UserId, Privileged);
        if (type != null) query = query.Where(a => a.ActivityType == type);
        if (status != null) query = query.Where(a => a.Status == status);
        if (from != null) query = query.Where(a => a.ActivityDate >= from.Value.Date);
        if (to != null) query = query.Where(a => a.ActivityDate < to.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(assignedTo)) query = query.Where(a => a.AssignedTo == assignedTo);
        ViewBag.Type = type; ViewBag.Status = status; ViewBag.From = from; ViewBag.To = to;
        await FillAllUsersForFilter(assignedTo);
        return View(await PagedList<Activity>.CreateAsync(query.OrderByDescending(a => a.ActivityDate), page, PageSize));
    }

    public async Task<IActionResult> Create(int? customerId, int? leadId)
    {
        await Fill(UserId, customerId, leadId);
        return View(new Activity { CustomerId = customerId, LeadId = leadId, AssignedTo = UserId });
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("ActivityType,Subject,Description,ActivityDate,CustomerId,LeadId,AssignedTo,Status")] Activity a)
    {
        if (!Privileged) { a.AssignedTo = UserId; ModelState.Remove(nameof(Activity.AssignedTo)); }
        if (ModelState.IsValid && await ValidateRefs(a) && await ValidateAssignee(a.AssignedTo))
        {
            Db.Activities.Add(a);
            await Db.SaveChangesAsync();
            await Audit.LogAsync("Create", "Activity", a.ActivityId.ToString(), newValue: AuditService.Snapshot(a));
            TempData["Success"] = "Activity logged.";
            return RedirectToAction(nameof(Index));
        }
        await Fill(a.AssignedTo, a.CustomerId, a.LeadId);
        return View(a);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var a = await Db.Activities.FindAsync(id);
        if (a == null) return NotFound();
        if (!a.CanAccess(UserId, Privileged)) return Forbid();
        await Fill(a.AssignedTo, a.CustomerId, a.LeadId);
        return View(a);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string assignedTo)
    {
        var a = await Db.Activities.FindAsync(id);
        if (a == null) return NotFound();
        if (!a.CanAccess(UserId, Privileged)) return Forbid();
        var old = AuditService.Snapshot(a);
        var ok = await TryUpdateModelAsync(a, "", x => x.ActivityType, x => x.Subject, x => x.Description, x => x.ActivityDate, x => x.CustomerId, x => x.LeadId, x => x.Status);
        if (ok) ok = await ValidateRefs(a);
        if (ok && Privileged && !string.IsNullOrEmpty(assignedTo) && assignedTo != a.AssignedTo)
        {
            if (await ValidateAssignee(assignedTo)) a.AssignedTo = assignedTo; else ok = false;
        }
        if (!ok) { await Fill(a.AssignedTo, a.CustomerId, a.LeadId); return View(a); }
        await Db.SaveChangesAsync();
        await Audit.LogAsync("Update", "Activity", id.ToString(), old, AuditService.Snapshot(a));
        TempData["Success"] = "Activity updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await Db.Activities.FindAsync(id);
        if (a == null) return NotFound();
        if (!a.CanAccess(UserId, Privileged)) return Forbid();
        var old = AuditService.Snapshot(a);
        Db.Activities.Remove(a);
        await Db.SaveChangesAsync();
        await Audit.LogAsync("Delete", "Activity", id.ToString(), old);
        TempData["Success"] = "Activity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task Fill(string assignee, int? c, int? l) { await FillUsers(assignee); await FillCustomers(c); await FillLeads(l); }

    private async Task<bool> ValidateRefs(Activity a)
    {
        if (a.CustomerId != null)
        {
            var c = await Db.Customers.FindAsync(a.CustomerId);
            if (c == null || !c.CanAccess(UserId, Privileged)) { ModelState.AddModelError(nameof(Activity.CustomerId), "Select a valid customer."); return false; }
        }
        if (a.LeadId != null)
        {
            var l = await Db.Leads.FindAsync(a.LeadId);
            if (l == null || !l.CanAccess(UserId, Privileged)) { ModelState.AddModelError(nameof(Activity.LeadId), "Select a valid lead."); return false; }
        }
        return true;
    }
}
