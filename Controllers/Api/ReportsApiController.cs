using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController, Route("api/reports"), Authorize(Roles = Roles.Privileged), IgnoreAntiforgeryToken]
public class ReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public ReportsApiController(ApplicationDbContext db) { _db = db; }

    /// <summary>Stage-wise and owner-wise pipeline (Admin/Manager).</summary>
    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline()
    {
        var rows = await _db.Opportunities.Include(o => o.AssignedUser).ToListAsync();
        var byStage = rows.GroupBy(o => o.Stage).Select(g => new { Stage = g.Key.ToString(), Count = g.Count(), Amount = g.Sum(x => x.Amount), Weighted = g.Sum(x => x.Weighted) });
        var byOwner = rows.Where(o => o.IsActiveStage).GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
            .Select(g => new { Owner = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount), Weighted = g.Sum(x => x.Weighted) });
        return Ok(new { byStage, byOwner });
    }
}
