using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public abstract class BaseController : Controller
{
    protected readonly ApplicationDbContext Db;
    protected readonly IAuditService Audit;
    protected const int PageSize = 10;

    protected BaseController(ApplicationDbContext db, IAuditService audit) { Db = db; Audit = audit; }

    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    protected bool Privileged => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Manager);

    protected async Task FillUsers(string selected = null)
    {
        var q = Db.Users.Where(u => u.IsActive);
        if (!Privileged) q = q.Where(u => u.Id == UserId);
        var list = await q.OrderBy(u => u.FullName).Select(u => new { u.Id, u.FullName }).ToListAsync();
        ViewBag.Users = new SelectList(list, "Id", "FullName", selected);
    }

    protected async Task FillAllUsersForFilter(string selected = null)
    {
        var list = await Db.Users.OrderBy(u => u.FullName).Select(u => new { u.Id, u.FullName }).ToListAsync();
        ViewBag.FilterUsers = new SelectList(list, "Id", "FullName", selected);
    }

    protected async Task FillCustomers(int? selected = null)
    {
        var list = await Db.Customers.ForUser(UserId, Privileged).Where(c => c.IsActive)
            .OrderBy(c => c.CustomerName).Select(c => new { c.CustomerId, Text = c.CustomerName + " (" + c.CustomerCode + ")" }).ToListAsync();
        ViewBag.Customers = new SelectList(list, "CustomerId", "Text", selected);
    }

    protected async Task FillLeads(int? selected = null)
    {
        var list = await Db.Leads.ForUser(UserId, Privileged).OrderBy(l => l.LeadName)
            .Select(l => new { l.LeadId, Text = l.LeadName + " (" + l.LeadCode + ")" }).ToListAsync();
        ViewBag.Leads = new SelectList(list, "LeadId", "Text", selected);
    }

    protected async Task FillOpportunities(int? selected = null)
    {
        var list = await Db.Opportunities.ForUser(UserId, Privileged).OrderBy(o => o.OpportunityName)
            .Select(o => new { o.OpportunityId, Text = o.OpportunityName }).ToListAsync();
        ViewBag.Opportunities = new SelectList(list, "OpportunityId", "Text", selected);
    }

    // Server-side check of the assignee: must exist, be active, and a Sales Executive may only assign to self.
    protected async Task<bool> ValidateAssignee(string assignedTo, string field = "AssignedTo")
    {
        if (!Privileged) return assignedTo == UserId;
        if (await Db.Users.AnyAsync(u => u.Id == assignedTo && u.IsActive)) return true;
        ModelState.AddModelError(field, "Selected user is invalid or inactive.");
        return false;
    }
}
