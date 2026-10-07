using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class OpportunitiesController : BaseController
{
    public OpportunitiesController(ApplicationDbContext db, IAuditService audit) : base(db, audit) { }

    public async Task<IActionResult> Index(string q, OpportunityStage? stage, string status, int? customerId, int page = 1)
    {
        var query = Db.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).ForUser(UserId, Privileged);
        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim().ToLower(); query = query.Where(o => o.OpportunityName.ToLower().Contains(q)); }
        if (stage != null) query = query.Where(o => o.Stage == stage);
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
        if (customerId != null) query = query.Where(o => o.CustomerId == customerId);
        ViewBag.Q = q; ViewBag.Stage = stage; ViewBag.StatusFilter = status;
        await FillCustomers(customerId);
        return View(await PagedList<Opportunity>.CreateAsync(query.OrderByDescending(o => o.OpportunityId), page, PageSize));
    }

    public async Task<IActionResult> Pipeline()
    {
        var rows = await Db.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser)
            .ForUser(UserId, Privileged).OrderBy(o => o.ExpectedCloseDate).ToListAsync();
        return View(rows);
    }

    public async Task<IActionResult> Details(int id)
    {
        var o = await Db.Opportunities.Include(x => x.Customer).Include(x => x.Lead).Include(x => x.AssignedUser)
                        .FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o == null) return NotFound();
        if (!o.CanAccess(UserId, Privileged)) return Forbid();
        ViewBag.FollowUps = await Db.FollowUps.Where(f => f.OpportunityId == id).OrderByDescending(f => f.FollowUpDate).ToListAsync();
        return View(o);
    }

    public async Task<IActionResult> Create(int? customerId)
    {
        await Fill(UserId, customerId, null);
        return View(new Opportunity { CustomerId = customerId ?? 0, AssignedTo = UserId });
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("OpportunityName,CustomerId,LeadId,Amount,Stage,Probability,ExpectedCloseDate,Source,Notes,AssignedTo")] Opportunity o)
    {
        if (!Privileged) { o.AssignedTo = UserId; ModelState.Remove(nameof(Opportunity.AssignedTo)); }
        o.ExpectedCloseDate = o.ExpectedCloseDate.Date;
        if (ModelState.IsValid && await ValidateRefs(o) && await ValidateAssignee(o.AssignedTo))
        {
            o.ApplyStageRules();
            o.CreatedDate = DateTime.Now;
            Db.Opportunities.Add(o);
            await Db.SaveChangesAsync();
            await Audit.LogAsync("Create", "Opportunity", o.OpportunityId.ToString(), newValue: AuditService.Snapshot(o));
            TempData["Success"] = "Opportunity created successfully.";
            return RedirectToAction(nameof(Details), new { id = o.OpportunityId });
        }
        await Fill(o.AssignedTo, o.CustomerId, o.LeadId);
        return View(o);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var o = await Db.Opportunities.FindAsync(id);
        if (o == null) return NotFound();
        if (!o.CanAccess(UserId, Privileged)) return Forbid();
        await Fill(o.AssignedTo, o.CustomerId, o.LeadId);
        return View(o);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string assignedTo)
    {
        var o = await Db.Opportunities.FindAsync(id);
        if (o == null) return NotFound();
        if (!o.CanAccess(UserId, Privileged)) return Forbid();
        var old = AuditService.Snapshot(o);
        var oldStage = o.Stage;

        var ok = await TryUpdateModelAsync(o, "", x => x.OpportunityName, x => x.CustomerId, x => x.LeadId, x => x.Amount,
                                           x => x.Stage, x => x.Probability, x => x.ExpectedCloseDate, x => x.Source, x => x.Notes);
        o.ExpectedCloseDate = o.ExpectedCloseDate.Date;
        if (ok) ok = await ValidateRefs(o);
        if (ok && Privileged && !string.IsNullOrEmpty(assignedTo) && assignedTo != o.AssignedTo)
        {
            if (await ValidateAssignee(assignedTo)) o.AssignedTo = assignedTo; else ok = false;
        }
        if (!ok) { await Fill(o.AssignedTo, o.CustomerId, o.LeadId); return View(o); }

        o.ApplyStageRules();
        await Db.SaveChangesAsync();
        await Audit.LogAsync(oldStage != o.Stage ? "StatusChange" : "Update", "Opportunity", id.ToString(), old, AuditService.Snapshot(o));
        TempData["Success"] = "Opportunity updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Privileged)]
    public async Task<IActionResult> Delete(int id)
    {
        var o = await Db.Opportunities.FindAsync(id);
        if (o == null) return NotFound();
        var old = AuditService.Snapshot(o);
        Db.Opportunities.Remove(o);
        await Db.SaveChangesAsync();
        await Audit.LogAsync("Delete", "Opportunity", id.ToString(), old);
        TempData["Success"] = "Opportunity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task Fill(string assignee, int? customerId, int? leadId)
    { await FillUsers(assignee); await FillCustomers(customerId); await FillLeads(leadId); }

    // Foreign keys must reference existing records the user is allowed to use.
    private async Task<bool> ValidateRefs(Opportunity o)
    {
        var c = await Db.Customers.FindAsync(o.CustomerId);
        if (c == null || !c.IsActive || !c.CanAccess(UserId, Privileged)) { ModelState.AddModelError(nameof(Opportunity.CustomerId), "Select a valid customer."); return false; }
        if (o.LeadId != null)
        {
            var l = await Db.Leads.FindAsync(o.LeadId);
            if (l == null || !l.CanAccess(UserId, Privileged)) { ModelState.AddModelError(nameof(Opportunity.LeadId), "Select a valid lead."); return false; }
        }
        return true;
    }
}
