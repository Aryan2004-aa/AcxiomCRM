using System.Text;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class ReportsController : BaseController
{
    public ReportsController(ApplicationDbContext db, IAuditService audit) : base(db, audit) { }

    public IActionResult Index() => View();

    // id: customers | leads | followups | opportunities | pipeline | conversion | useractivity | audit
    public async Task<IActionResult> Run(string id, DateTime? from, DateTime? to, int? sort, int page = 1, bool export = false)
    {
        var uid = UserId; var priv = Privileged;
        var f = from?.Date; var t = to?.Date.AddDays(1);
        string title; string[] cols; List<string[]> rows;

        switch (id)
        {
            case "customers":
            {
                title = "Customer Report"; cols = new[] { "Code", "Customer", "Status", "Owner", "Created" };
                var q = Db.Customers.Include(c => c.AssignedUser).ForUser(uid, priv);
                if (f != null) q = q.Where(c => c.CreatedDate >= f && c.CreatedDate < t);
                rows = (await q.ToListAsync()).Select(c => new[] { c.CustomerCode, c.CustomerName, c.Status.ToString(), c.AssignedUser?.FullName, c.CreatedDate.ToString("yyyy-MM-dd") }).ToList();
                break;
            }
            case "leads":
            {
                title = "Lead Report"; cols = new[] { "Code", "Lead", "Source", "Status", "Owner", "Expected Value", "Converted" };
                var q = Db.Leads.Include(l => l.AssignedUser).ForUser(uid, priv);
                if (f != null) q = q.Where(l => l.CreatedDate >= f && l.CreatedDate < t);
                rows = (await q.ToListAsync()).Select(l => new[] { l.LeadCode, l.LeadName, l.Source, l.Status.ToString(), l.AssignedUser?.FullName, l.ExpectedValue.ToString("0.00"), l.Status == LeadStatus.Converted ? "Yes" : "No" }).ToList();
                break;
            }
            case "followups":
            {
                title = "Follow-Up Report"; cols = new[] { "Date", "Subject", "Type", "Status", "Owner", "Overdue" };
                var q = Db.FollowUps.Include(x => x.AssignedUser).ForUser(uid, priv);
                if (f != null) q = q.Where(x => x.FollowUpDate >= f && x.FollowUpDate < t);
                rows = (await q.ToListAsync()).Select(x => new[] { x.FollowUpDate.ToString("yyyy-MM-dd"), x.Subject, x.FollowUpType.ToString(), x.Status.ToString(), x.AssignedUser?.FullName, x.IsOverdue ? "Yes" : "No" }).ToList();
                break;
            }
            case "opportunities":
            {
                title = "Opportunity Report"; cols = new[] { "Opportunity", "Customer", "Stage", "Amount", "Probability %", "Expected Close", "Owner" };
                var q = Db.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).ForUser(uid, priv);
                if (f != null) q = q.Where(o => o.CreatedDate >= f && o.CreatedDate < t);
                rows = (await q.ToListAsync()).Select(o => new[] { o.OpportunityName, o.Customer?.CustomerName, o.Stage.ToString(), o.Amount.ToString("0.00"), o.Probability.ToString(), o.ExpectedCloseDate.ToString("yyyy-MM-dd"), o.AssignedUser?.FullName }).ToList();
                break;
            }
            case "pipeline":
            {
                title = "Pipeline Report (stage-wise & owner-wise)"; cols = new[] { "Stage", "Owner", "Count", "Amount", "Weighted Amount" };
                var q = Db.Opportunities.Include(o => o.AssignedUser).ForUser(uid, priv);
                if (f != null) q = q.Where(o => o.CreatedDate >= f && o.CreatedDate < t);
                rows = (await q.ToListAsync()).GroupBy(o => new { o.Stage, Owner = o.AssignedUser?.FullName })
                    .Select(g => new[] { g.Key.Stage.ToString(), g.Key.Owner, g.Count().ToString(), g.Sum(x => x.Amount).ToString("0.00"), g.Sum(x => x.Weighted).ToString("0.00") }).ToList();
                break;
            }
            case "conversion":
            {
                title = "Sales / Conversion Report"; cols = new[] { "Metric", "Value" };
                var leads = Db.Leads.ForUser(uid, priv); var opps = Db.Opportunities.ForUser(uid, priv);
                if (f != null) { leads = leads.Where(l => l.CreatedDate >= f && l.CreatedDate < t); opps = opps.Where(o => o.CreatedDate >= f && o.CreatedDate < t); }
                var ls = await leads.Select(l => l.Status).ToListAsync();
                var os = await opps.Select(o => new { o.Stage, o.Amount }).ToListAsync();
                var conv = ls.Count(s => s == LeadStatus.Converted);
                var won = os.Where(o => o.Stage == OpportunityStage.Won).ToList(); var lost = os.Count(o => o.Stage == OpportunityStage.Lost);
                rows = new List<string[]>
                {
                    new[] { "Total leads", ls.Count.ToString() }, new[] { "Converted leads", conv.ToString() },
                    new[] { "Not converted", (ls.Count - conv).ToString() },
                    new[] { "Lead conversion rate %", ls.Count == 0 ? "0" : (conv * 100.0 / ls.Count).ToString("0.0") },
                    new[] { "Opportunities won", won.Count.ToString() }, new[] { "Opportunities lost", lost.ToString() },
                    new[] { "Win rate % (won / closed)", won.Count + lost == 0 ? "0" : (won.Count * 100.0 / (won.Count + lost)).ToString("0.0") },
                    new[] { "Won amount", won.Sum(o => o.Amount).ToString("0.00") }
                };
                break;
            }
            case "useractivity":
            {
                if (!priv) return Forbid();
                title = "User Activity Report"; cols = new[] { "User", "Total Actions", "Logins", "Creates", "Updates", "Deletes", "Failures" };
                var q = Db.AuditLogs.AsQueryable();
                if (f != null) q = q.Where(a => a.CreatedDate >= f && a.CreatedDate < t);
                rows = (await q.ToListAsync()).GroupBy(a => a.UserName ?? "(anonymous)").Select(g => new[] { g.Key, g.Count().ToString(),
                    g.Count(a => a.Action == "Login").ToString(), g.Count(a => a.Action == "Create").ToString(), g.Count(a => a.Action == "Update").ToString(),
                    g.Count(a => a.Action == "Delete").ToString(), g.Count(a => a.Result == "Failure").ToString() }).ToList();
                break;
            }
            case "audit":
            {
                if (!User.IsInRole(Roles.Admin)) return Forbid();
                title = "Audit Report"; cols = new[] { "Time", "User", "Action", "Module", "Record", "Result", "IP" };
                var q = Db.AuditLogs.AsQueryable();
                if (f != null) q = q.Where(a => a.CreatedDate >= f && a.CreatedDate < t);
                rows = (await q.OrderByDescending(a => a.AuditLogId).ToListAsync()).Select(a => new[] { a.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"), a.UserName, a.Action, a.EntityName, a.RecordId, a.Result, a.IpAddress }).ToList();
                break;
            }
            default: return NotFound();
        }

        if (sort is int s && s >= 0 && s < cols.Length)
            rows = rows.OrderBy(r => r[s], StringComparer.OrdinalIgnoreCase).ToList();

        if (export)
        {
            await Audit.LogAsync("Export", "Report", id);
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", cols.Select(Csv)));
            foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(Csv)));
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", $"{id}-report-{DateTime.Now:yyyyMMdd}.csv");
        }

        const int size = 15;
        var total = rows.Count;
        var vm = new ReportVm
        {
            Title = title, Action = id, Columns = cols, From = from, To = to, Sort = sort, Total = total,
            Page = Math.Max(1, page), TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)size))
        };
        vm.Rows = rows.Skip((vm.Page - 1) * size).Take(size).ToList();
        return View(vm);
    }

    // Prevents CSV/Excel formula injection.
    private static string Csv(string v)
    {
        v ??= "";
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
