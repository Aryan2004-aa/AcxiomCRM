using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class HomeController : BaseController
{
    public HomeController(ApplicationDbContext db, IAuditService audit) : base(db, audit) { }

    public async Task<IActionResult> Index(string range = "all", DateTime? from = null, DateTime? to = null)
    {
        DateTime? f = null, t = null;
        var today = DateTime.Today;
        switch (range)
        {
            case "today": f = today; t = today.AddDays(1); break;
            case "week": f = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); t = f.Value.AddDays(7); break;
            case "month": f = new DateTime(today.Year, today.Month, 1); t = f.Value.AddMonths(1); break;
            case "custom": if (from.HasValue && to.HasValue) { f = from.Value.Date; t = to.Value.Date.AddDays(1); } break;
            default: range = "all"; break;
        }

        var uid = UserId; var priv = Privileged;
        var customers = Db.Customers.ForUser(uid, priv).Where(c => c.IsActive);
        var leads = Db.Leads.ForUser(uid, priv);
        var opps = Db.Opportunities.ForUser(uid, priv);
        var fups = Db.FollowUps.ForUser(uid, priv);
        if (f != null)
        {
            customers = customers.Where(c => c.CreatedDate >= f && c.CreatedDate < t);
            leads = leads.Where(l => l.CreatedDate >= f && l.CreatedDate < t);
            opps = opps.Where(o => o.CreatedDate >= f && o.CreatedDate < t);
            fups = fups.Where(x => x.FollowUpDate >= f && x.FollowUpDate < t);
        }

        var vm = new DashboardVm { Range = range, From = from, To = to, IsAdmin = User.IsInRole(Roles.Admin) };
        vm.TotalCustomers = await customers.CountAsync();

        var leadStatuses = await leads.Select(l => l.Status).ToListAsync();
        vm.TotalLeads = leadStatuses.Count;
        vm.OpenLeads = leadStatuses.Count(s => s != LeadStatus.Lost && s != LeadStatus.Converted && s != LeadStatus.Unqualified);
        foreach (var s in new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Lost, LeadStatus.Converted })
        { vm.LeadLabels.Add(s.ToString()); vm.LeadCounts.Add(leadStatuses.Count(x => x == s)); }

        // Decimal maths is done in memory (SQLite cannot aggregate decimals).
        var oppRows = await opps.Select(o => new { o.Stage, o.Amount, o.Probability }).ToListAsync();
        vm.TotalOpportunities = oppRows.Count;
        var open = oppRows.Where(o => o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost).ToList();
        vm.OpenOpportunities = open.Count;
        vm.WonOpportunities = oppRows.Count(o => o.Stage == OpportunityStage.Won);
        vm.LostOpportunities = oppRows.Count(o => o.Stage == OpportunityStage.Lost);
        vm.PipelineValue = open.Sum(o => o.Amount);
        vm.WeightedPipeline = open.Sum(o => o.Amount * o.Probability / 100m);
        foreach (var s in Enum.GetValues<OpportunityStage>())
        { vm.StageLabels.Add(s.ToString()); vm.StageCounts.Add(oppRows.Count(o => o.Stage == s)); }

        vm.PendingFollowUps = await fups.CountAsync(x => x.Status == FollowUpStatus.Planned);
        vm.OverdueFollowUps = await fups.CountAsync(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate < today);

        // Monthly sales: Won amounts for the last 6 months (scoped to the user).
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
        var won = await Db.Opportunities.ForUser(uid, priv)
            .Where(o => o.Stage == OpportunityStage.Won && o.ClosedDate >= start)
            .Select(o => new { o.ClosedDate, o.Amount }).ToListAsync();
        for (var i = 0; i < 6; i++)
        {
            var m = start.AddMonths(i);
            vm.MonthLabels.Add(m.ToString("MMM yyyy"));
            vm.MonthSales.Add(won.Where(w => w.ClosedDate.Value.Year == m.Year && w.ClosedDate.Value.Month == m.Month).Sum(w => w.Amount));
        }

        if (vm.IsAdmin)
        {
            vm.TotalUsers = await Db.Users.CountAsync();
            vm.LockedUsers = (await Db.Users.Where(u => u.LockoutEnd != null).Select(u => u.LockoutEnd).ToListAsync()).Count(x => x > DateTimeOffset.UtcNow);
            vm.FailedLoginsToday = await Db.AuditLogs.CountAsync(a => a.Action == "LoginFailed" && a.CreatedDate >= today);
        }
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
