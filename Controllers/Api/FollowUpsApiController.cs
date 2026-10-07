using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

public class FollowUpDto
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }
    public DateTime FollowUpDate { get; set; }
    [Required, StringLength(150)] public string Subject { get; set; }
    [EnumDataType(typeof(ActivityType))] public ActivityType FollowUpType { get; set; }
    [StringLength(1000)] public string Remarks { get; set; }
    public FollowUpStatus Status { get; set; }
    public string AssignedTo { get; set; }
}

[ApiController, Route("api/followups"), Authorize, IgnoreAntiforgeryToken]
public class FollowUpsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    public FollowUpsApiController(ApplicationDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool Priv => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Manager);
    private static FollowUpDto ToDto(FollowUp f) => new() { Id = f.FollowUpId, CustomerId = f.CustomerId, LeadId = f.LeadId, OpportunityId = f.OpportunityId, FollowUpDate = f.FollowUpDate, Subject = f.Subject, FollowUpType = f.FollowUpType, Remarks = f.Remarks, Status = f.Status, AssignedTo = f.AssignedTo };

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FollowUpDto>>> GetAll(FollowUpStatus? status = null, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.FollowUps.ForUser(Uid, Priv);
        if (status != null) q = q.Where(f => f.Status == status);
        var items = await q.OrderBy(f => f.FollowUpDate).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FollowUpDto>> Get(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f == null) return NotFound();
        if (!f.CanAccess(Uid, Priv)) return Forbid();
        return ToDto(f);
    }

    [HttpPost]
    public async Task<ActionResult<FollowUpDto>> Create(FollowUpDto dto)
    {
        var f = new FollowUp
        {
            CustomerId = dto.CustomerId, LeadId = dto.LeadId, OpportunityId = dto.OpportunityId, FollowUpDate = dto.FollowUpDate.Date,
            Subject = dto.Subject, FollowUpType = dto.FollowUpType, Remarks = dto.Remarks, Status = FollowUpStatus.Planned,
            AssignedTo = Priv && !string.IsNullOrEmpty(dto.AssignedTo) ? dto.AssignedTo : Uid
        };
        // Re-run full model validation (date rule, related-record rule) on the entity.
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(f, new ValidationContext(f), results, true))
        {
            var errs = results.GroupBy(r => r.MemberNames.FirstOrDefault() ?? "").ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage).ToArray());
            return BadRequest(new ValidationProblemDetails(errs));
        }
        if (!await _db.Users.AnyAsync(u => u.Id == f.AssignedTo && u.IsActive))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["AssignedTo"] = new[] { "Selected user is invalid or inactive." } }));
        if (f.CustomerId != null)
        {
            var c = await _db.Customers.FindAsync(f.CustomerId);
            if (c == null || !c.CanAccess(Uid, Priv)) return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["CustomerId"] = new[] { "Select a valid customer." } }));
        }
        if (f.LeadId != null)
        {
            var l = await _db.Leads.FindAsync(f.LeadId);
            if (l == null || !l.CanAccess(Uid, Priv)) return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["LeadId"] = new[] { "Select a valid lead." } }));
        }
        if (f.OpportunityId != null)
        {
            var o = await _db.Opportunities.FindAsync(f.OpportunityId);
            if (o == null || !o.CanAccess(Uid, Priv)) return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["OpportunityId"] = new[] { "Select a valid opportunity." } }));
        }
        _db.FollowUps.Add(f);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "FollowUp", f.FollowUpId.ToString(), newValue: AuditService.Snapshot(f));
        return CreatedAtAction(nameof(Get), new { id = f.FollowUpId }, ToDto(f));
    }
}
