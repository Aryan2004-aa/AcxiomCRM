using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<Enum>().HaveConversion<string>();
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => x.CustomerCode).IsUnique();
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Lead>(e =>
        {
            e.HasIndex(x => x.LeadCode).IsUnique();
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Opportunity>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<FollowUp>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.IsOverdue);
        });

        b.Entity<Activity>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>(e => { e.HasIndex(x => x.CreatedDate); e.HasIndex(x => x.UserId); });
    }

    // Audit records are append-only.
    private void GuardAudit()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Modified || e.State == EntityState.Deleted))
            throw new InvalidOperationException("Audit log records are append-only and cannot be modified or deleted.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
