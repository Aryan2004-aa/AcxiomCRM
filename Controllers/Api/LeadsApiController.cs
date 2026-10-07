using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController, Route("api/leads"), Authorize, IgnoreAntiforgeryToken]
public class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly LeadService _svc;
    private readonly IAuditService _audit;
    public LeadsApiController(ApplicationDbContext db, LeadService svc, IAuditService audit) { _db = db; _svc = svc; _audit = audit; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool Priv => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Manager);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetAll(string q = null, LeadStatus? status = null, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Leads.ForUser(Uid, Priv);
        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim().ToLower(); query = query.Where(l => l.LeadName.ToLower().Contains(q) || (l.CompanyName ?? "").ToLower().Contains(q)); }
        if (status != null) query = query.Where(l => l.Status == status);
        Response.Headers["X-Total-Count"] = (await query.CountAsync()).ToString();
        var items = await query.OrderBy(l => l.LeadId).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(items.Select(LeadDto.From));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadDto>> Get(int id)
    {
        var l = await _db.Leads.FindAsync(id);
        if (l == null) return NotFound();
        if (!l.CanAccess(Uid, Priv)) return Forbid();
        return LeadDto.From(l);
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create(LeadDto dto)
    {
        if (!Lead.Sources.Contains(dto.Source))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Source"] = new[] { "Select a valid lead source." } }));
        var l = new Lead();
        dto.Apply(l);
        l.AssignedTo = Priv && !string.IsNullOrEmpty(dto.AssignedTo) ? dto.AssignedTo : Uid;
        if (!await _db.Users.AnyAsync(u => u.Id == l.AssignedTo && u.IsActive))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["AssignedTo"] = new[] { "Selected user is invalid or inactive." } }));
        LeadService.Normalize(l);
        await _svc.CreateAsync(l);   // status always starts as New
        await _audit.LogAsync("Create", "Lead", l.LeadId.ToString(), newValue: AuditService.Snapshot(l));
        return CreatedAtAction(nameof(Get), new { id = l.LeadId }, LeadDto.From(l));
    }
}
