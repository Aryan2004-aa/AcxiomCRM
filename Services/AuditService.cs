using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entity, string recordId, string oldValue = null, string newValue = null,
                  string result = "Success", string userId = null, string userName = null);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private static readonly JsonSerializerOptions Opts = new() { Converters = { new JsonStringEnumConverter() } };

    public AuditService(ApplicationDbContext db, IHttpContextAccessor http) { _db = db; _http = http; }

    public async Task LogAsync(string action, string entity, string recordId, string oldValue = null, string newValue = null,
                               string result = "Success", string userId = null, string userName = null)
    {
        var ctx = _http.HttpContext;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? ctx?.User?.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = userName ?? ctx?.User?.Identity?.Name,
            Action = action,
            EntityName = entity,
            RecordId = recordId,
            OldValue = oldValue,
            NewValue = newValue,
            Result = result,
            CreatedDate = DateTime.Now,
            IpAddress = ctx?.Connection?.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
    }

    // Serialises scalar properties only (no navigations => no password hashes or secrets can leak into the audit log).
    public static string Snapshot(object entity)
    {
        if (entity == null) return null;
        var d = new Dictionary<string, object>();
        foreach (var p in entity.GetType().GetProperties())
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime))
                d[p.Name] = p.GetValue(entity);
        }
        return JsonSerializer.Serialize(d, Opts);
    }
}
