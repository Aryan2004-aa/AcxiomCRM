using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class FollowUpsController : BaseController
{
    public FollowUpsController(ApplicationDbContext db, IAuditService audit) : base(db, audit) { }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, FollowUpStatus? status, string assignedTo,
                                           int? customerId, int? leadId, bool overdue = false, int page = 1)
    {
        var query = Db.FollowUps.Include(f => f.Customer).Include(f => f.Lead).Include(f => f.Opportunity).Include(f => f.AssignedUser)
                      .ForUser(UserId, Privileged);
        if (from != null) query = query.Where(f => f.FollowUpDate >= from.Value.Date);
        if (to != null) query = query.Where(f => f.FollowUpDate < to.Value.Date.AddDays(1));
        if (status != null) query = query.Where(f => f.Status == status);
        if (!string.IsNullOrEmpty(assignedTo)) query = query.Where(f => f.AssignedTo == assignedTo);
        if (customerId != null) query = query.Where(f => f.CustomerId == customerId);
        if (leadId != null) query = query.Where(f => f.LeadId == leadId);
        var today = DateTime.Today;
        if (overdue) query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < today);
        ViewBag.From = from; ViewBag.To = to; ViewBag.Status = status; ViewBag.Overdue = overdue;
        await FillAllUsersForFilter(assignedTo); await FillCustomers(customerId); await FillLeads(leadId);
        return View("Index", await PagedList<FollowUp>.CreateAsync(query.OrderBy(f => f.FollowUpDate), page, PageSize));
    }

    // Pending = planned follow-ups.
    public Task<IActionResult> Pending(int page = 1) => Index(null, null, FollowUpStatus.Planned, null, null, null, false, page);

    public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
    {
        await Fill(UserId, customerId, leadId, opportunityId);
        return View(new FollowUp { CustomerId = customerId, LeadId = leadId, OpportunityId = opportunityId, AssignedTo = UserId });
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("CustomerId,LeadId,OpportunityId,FollowUpDate,Subject,FollowUpType,Remarks,AssignedTo")] FollowUp f)
    {
        if (!Privileged) { f.AssignedTo = UserId; ModelState.Remove(nameof(FollowUp.AssignedTo)); }
        f.FollowUpDate = f.FollowUpDate.Date;
        f.Status = FollowUpStatus.Planned;
        if (ModelState.IsValid && await ValidateRefs(f) && await ValidateAssignee(f.AssignedTo))
        {
            Db.FollowUps.Add(f);
            await Db.SaveChangesAsync();
            await Audit.LogAsync("Create", "FollowUp", f.FollowUpId.ToString(), newValue: AuditService.Snapshot(f));
            TempData["Success"] = "Follow-up scheduled.";
            return RedirectToAction(nameof(Index));
        }
        await Fill(f.AssignedTo, f.CustomerId, f.LeadId, f.OpportunityId);
        return View(f);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var f = await Db.FollowUps.FindAsync(id);
        if (f == null) return NotFound();
        if (!f.CanAccess(UserId, Privileged)) return Forbid();
        await Fill(f.AssignedTo, f.CustomerId, f.LeadId, f.OpportunityId);
        return View(f);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string assignedTo)
    {
        var f = await Db.FollowUps.FindAsync(id);
        if (f == null) return NotFound();
        if (!f.CanAccess(UserId, Privileged)) return Forbid();
        var old = AuditService.Snapshot(f);
        var oldDate = f.FollowUpDate; var oldStatus = f.Status;

        var ok = await TryUpdateModelAsync(f, "", x => x.FollowUpDate, x => x.Subject, x => x.FollowUpType, x => x.Remarks, x => x.Status);
        f.FollowUpDate = f.FollowUpDate.Date;
        if (ok && Privileged && !string.IsNullOrEmpty(assignedTo) && assignedTo != f.AssignedTo)
        {
            if (await ValidateAssignee(assignedTo)) f.AssignedTo = assignedTo; else ok = false;
        }
        if (!ok) { await Fill(f.AssignedTo, f.CustomerId, f.LeadId, f.OpportunityId); return View(f); }

        await Db.SaveChangesAsync();
        var action = oldStatus != f.Status ? f.Status.ToString() : oldDate != f.FollowUpDate ? "Reschedule" : "Update";
        await Audit.LogAsync(action, "FollowUp", id.ToString(), old, AuditService.Snapshot(f));
        TempData["Success"] = "Follow-up updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, FollowUpStatus status)
    {
        var f = await Db.FollowUps.FindAsync(id);
        if (f == null) return NotFound();
        if (!f.CanAccess(UserId, Privileged)) return Forbid();
        if (status == FollowUpStatus.Planned || f.Status != FollowUpStatus.Planned)
        {
            TempData["Error"] = "Only planned follow-ups can be completed, marked missed or cancelled.";
            return RedirectToAction(nameof(Index));
        }
        var old = AuditService.Snapshot(f);
        f.Status = status;
        await Db.SaveChangesAsync();
        await Audit.LogAsync(status == FollowUpStatus.Completed ? "FollowUpCompleted" : "FollowUp" + status, "FollowUp", id.ToString(), old, AuditService.Snapshot(f));
        TempData["Success"] = $"Follow-up marked {status}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reschedule(int id, DateTime newDate)
    {
        var f = await Db.FollowUps.FindAsync(id);
        if (f == null) return NotFound();
        if (!f.CanAccess(UserId, Privileged)) return Forbid();
        if (newDate.Date < DateTime.Today)
        {
            TempData["Error"] = "Follow-up date cannot be earlier than today.";
            return RedirectToAction(nameof(Index));
        }
        var old = AuditService.Snapshot(f);
        f.FollowUpDate = newDate.Date; f.Status = FollowUpStatus.Planned;
        await Db.SaveChangesAsync();
        await Audit.LogAsync("Reschedule", "FollowUp", id.ToString(), old, AuditService.Snapshot(f));
        TempData["Success"] = "Follow-up rescheduled.";
        return RedirectToAction(nameof(Index));
    }

    private async Task Fill(string assignee, int? c, int? l, int? o)
    { await FillUsers(assignee); await FillCustomers(c); await FillLeads(l); await FillOpportunities(o); }

    private async Task<bool> ValidateRefs(FollowUp f)
    {
        if (f.CustomerId != null)
        {
            var c = await Db.Customers.FindAsync(f.CustomerId);
            if (c == null || !c.CanAccess(UserId, Privileged)) { ModelState.AddModelError(nameof(FollowUp.CustomerId), "Select a valid customer."); return false; }
        }
        if (f.LeadId != null)
        {
            var l = await Db.Leads.FindAsync(f.LeadId);
            if (l == null || !l.CanAccess(UserId, Privileged)) { ModelState.AddModelError(nameof(FollowUp.LeadId), "Select a valid lead."); return false; }
        }
        if (f.OpportunityId != null)
        {
            var o = await Db.Opportunities.FindAsync(f.OpportunityId);
            if (o == null || !o.CanAccess(UserId, Privileged)) { ModelState.AddModelError(nameof(FollowUp.OpportunityId), "Select a valid opportunity."); return false; }
        }
        return true;
    }
}
