using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController, Route("api/opportunities"), Authorize, IgnoreAntiforgeryToken]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    public OpportunitiesApiController(ApplicationDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool Priv => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Manager);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetAll(string q = null, OpportunityStage? stage = null, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Opportunities.ForUser(Uid, Priv);
        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim().ToLower(); query = query.Where(o => o.OpportunityName.ToLower().Contains(q)); }
        if (stage != null) query = query.Where(o => o.Stage == stage);
        Response.Headers["X-Total-Count"] = (await query.CountAsync()).ToString();
        var items = await query.OrderBy(o => o.OpportunityId).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(items.Select(OpportunityDto.From));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OpportunityDto>> Get(int id)
    {
        var o = await _db.Opportunities.FindAsync(id);
        if (o == null) return NotFound();
        if (!o.CanAccess(Uid, Priv)) return Forbid();
        return OpportunityDto.From(o);
    }

    [HttpPost]
    public async Task<ActionResult<OpportunityDto>> Create(OpportunityDto dto)
    {
        var customer = await _db.Customers.FindAsync(dto.CustomerId);
        if (customer == null || !customer.IsActive || !customer.CanAccess(Uid, Priv))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["CustomerId"] = new[] { "Select a valid customer." } }));
        if (dto.LeadId != null)
        {
            var lead = await _db.Leads.FindAsync(dto.LeadId);
            if (lead == null || !lead.CanAccess(Uid, Priv))
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["LeadId"] = new[] { "Select a valid lead." } }));
        }
        var o = new Opportunity();
        dto.Apply(o);
        o.AssignedTo = Priv && !string.IsNullOrEmpty(dto.AssignedTo) ? dto.AssignedTo : Uid;
        if (!await _db.Users.AnyAsync(u => u.Id == o.AssignedTo && u.IsActive))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["AssignedTo"] = new[] { "Selected user is invalid or inactive." } }));
        _db.Opportunities.Add(o);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Opportunity", o.OpportunityId.ToString(), newValue: AuditService.Snapshot(o));
        return CreatedAtAction(nameof(Get), new { id = o.OpportunityId }, OpportunityDto.From(o));
    }
}
