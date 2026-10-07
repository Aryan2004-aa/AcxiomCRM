using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class CustomersController : BaseController
{
    private readonly CustomerService _svc;
    public CustomersController(ApplicationDbContext db, IAuditService audit, CustomerService svc) : base(db, audit) { _svc = svc; }

    public async Task<IActionResult> Index(string q, CustomerStatus? status, int page = 1)
    {
        var query = Db.Customers.Include(c => c.AssignedUser).ForUser(UserId, Privileged);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim().ToLower();
            query = query.Where(c => c.CustomerName.ToLower().Contains(q) || c.Email.ToLower().Contains(q)
                                  || c.Phone.Contains(q) || (c.CompanyName ?? "").ToLower().Contains(q));
        }
        if (status != null) query = query.Where(c => c.Status == status);
        ViewBag.Q = q; ViewBag.Status = status;
        return View(await PagedList<Customer>.CreateAsync(query.OrderByDescending(c => c.CustomerId), page, PageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var c = await Db.Customers.Include(x => x.AssignedUser).FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound();
        if (!c.CanAccess(UserId, Privileged)) return Forbid();
        ViewBag.Opps = await Db.Opportunities.Where(o => o.CustomerId == id).OrderByDescending(o => o.OpportunityId).ToListAsync();
        ViewBag.FollowUps = await Db.FollowUps.Where(f => f.CustomerId == id).OrderByDescending(f => f.FollowUpDate).ToListAsync();
        ViewBag.Activities = await Db.Activities.Where(a => a.CustomerId == id).OrderByDescending(a => a.ActivityDate).ToListAsync();
        var rid = id.ToString();
        ViewBag.History = await Db.AuditLogs.Where(a => a.EntityName == "Customer" && a.RecordId == rid)
            .OrderByDescending(a => a.AuditLogId).Take(20).ToListAsync();
        return View(c);
    }

    public async Task<IActionResult> Create()
    {
        await FillUsers(UserId);
        return View(new Customer { AssignedTo = UserId });
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("CustomerName,Email,Phone,CompanyName,Address,City,State,Status,Notes,AssignedTo")] Customer c)
    {
        if (!Privileged) { c.AssignedTo = UserId; ModelState.Remove(nameof(Customer.AssignedTo)); }
        CustomerService.Normalize(c);
        if (ModelState.IsValid) await ValidateAssignee(c.AssignedTo);
        if (ModelState.IsValid)
            foreach (var d in await _svc.DuplicatesAsync(c, 0)) ModelState.AddModelError(d.Key, d.Value);
        if (!ModelState.IsValid) { await FillUsers(c.AssignedTo); return View(c); }

        await _svc.CreateAsync(c, UserId);
        await Audit.LogAsync("Create", "Customer", c.CustomerId.ToString(), newValue: AuditService.Snapshot(c));
        TempData["Success"] = "Customer created successfully.";
        return RedirectToAction(nameof(Details), new { id = c.CustomerId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await Db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        if (!c.CanAccess(UserId, Privileged)) return Forbid();
        await FillUsers(c.AssignedTo);
        return View(c);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string assignedTo)
    {
        var c = await Db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        if (!c.CanAccess(UserId, Privileged)) return Forbid();
        var old = AuditService.Snapshot(c);
        var oldStatus = c.Status;

        var ok = await TryUpdateModelAsync(c, "", x => x.CustomerName, x => x.Email, x => x.Phone, x => x.CompanyName,
                                           x => x.Address, x => x.City, x => x.State, x => x.Status, x => x.Notes);
        CustomerService.Normalize(c);
        if (ok && Privileged && !string.IsNullOrEmpty(assignedTo) && assignedTo != c.AssignedTo)
        {
            if (await ValidateAssignee(assignedTo)) c.AssignedTo = assignedTo; else ok = false;
        }
        if (ok) foreach (var d in await _svc.DuplicatesAsync(c, id)) { ModelState.AddModelError(d.Key, d.Value); ok = false; }
        if (!ok) { await FillUsers(c.AssignedTo); return View(c); }

        c.ModifiedDate = DateTime.Now;
        c.IsActive = c.Status != CustomerStatus.Inactive;
        await Db.SaveChangesAsync();
        await Audit.LogAsync(oldStatus != c.Status ? "StatusChange" : "Update", "Customer", id.ToString(), old, AuditService.Snapshot(c));
        TempData["Success"] = "Customer updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // "Delete" = deactivate (soft delete); keeps history intact.
    [HttpPost, Authorize(Roles = Roles.Privileged)]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await Db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        var old = AuditService.Snapshot(c);
        c.IsActive = false; c.Status = CustomerStatus.Inactive; c.ModifiedDate = DateTime.Now;
        await Db.SaveChangesAsync();
        await Audit.LogAsync("Delete", "Customer", id.ToString(), old, AuditService.Snapshot(c));
        TempData["Success"] = "Customer deactivated.";
        return RedirectToAction(nameof(Index));
    }
}
