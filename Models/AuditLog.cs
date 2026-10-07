namespace AcxiomCRM.Models;

// Append-only: ApplicationDbContext rejects updates/deletes of this entity.
public class AuditLog
{
    public int AuditLogId { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string Action { get; set; }
    public string EntityName { get; set; }
    public string RecordId { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string Result { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string IpAddress { get; set; }
}
