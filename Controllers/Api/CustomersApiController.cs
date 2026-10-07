using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

/// <summary>Customers API. 200/201/204 success; 400 validation; 401 unauthenticated; 403 forbidden; 404 not found; 409 duplicate.</summary>
[ApiController, Route("api/customers"), Authorize, IgnoreAntiforgeryToken]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CustomerService _svc;
    private readonly IAuditService _audit;
    public CustomersApiController(ApplicationDbContext db, CustomerService svc, IAuditService audit) { _db = db; _svc = svc; _audit = audit; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool Priv => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Manager);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetAll(string q = null, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Customers.ForUser(Uid, Priv);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim().ToLower();
            query = query.Where(c => c.CustomerName.ToLower().Contains(q) || c.Email.ToLower().Contains(q) || c.Phone.Contains(q));
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(c => c.CustomerId).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToListAsync();
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items.Select(CustomerDto.From));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Get(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        if (!c.CanAccess(Uid, Priv)) return Forbid();
        return CustomerDto.From(c);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CustomerDto dto)
    {
        var c = new Customer();
        dto.Apply(c);
        c.AssignedTo = Priv && !string.IsNullOrEmpty(dto.AssignedTo) ? dto.AssignedTo : Uid;
        if (!await _db.Users.AnyAsync(u => u.Id == c.AssignedTo && u.IsActive))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["AssignedTo"] = new[] { "Selected user is invalid or inactive." } }));
        CustomerService.Normalize(c);
        var dups = await _svc.DuplicatesAsync(c, 0);
        if (dups.Any()) return Conflict(new ProblemDetails { Status = 409, Title = "Duplicate customer.", Detail = string.Join(" ", dups.Values) });
        await _svc.CreateAsync(c, Uid);
        await _audit.LogAsync("Create", "Customer", c.CustomerId.ToString(), newValue: AuditService.Snapshot(c));
        return CreatedAtAction(nameof(Get), new { id = c.CustomerId }, CustomerDto.From(c));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Update(int id, CustomerDto dto)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        if (!c.CanAccess(Uid, Priv)) return Forbid();
        var old = AuditService.Snapshot(c);
        dto.Apply(c);
        CustomerService.Normalize(c);
        c.ModifiedDate = DateTime.Now;
        c.IsActive = c.Status != CustomerStatus.Inactive;
        var dups = await _svc.DuplicatesAsync(c, id);
        if (dups.Any()) { _db.Entry(c).State = EntityState.Unchanged; return Conflict(new ProblemDetails { Status = 409, Title = "Duplicate customer.", Detail = string.Join(" ", dups.Values) }); }
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Customer", id.ToString(), old, AuditService.Snapshot(c));
        return CustomerDto.From(c);
    }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Privileged)]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        var old = AuditService.Snapshot(c);
        c.IsActive = false; c.Status = CustomerStatus.Inactive; c.ModifiedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Customer", id.ToString(), old, AuditService.Snapshot(c));
        return NoContent();
    }
}
