using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public static class DbSeeder
{
    // Demo credentials - change/remove before any real deployment.
    public const string AdminEmail = "admin@acxiomcrm.com", AdminPassword = "Admin@12345";
    public const string ManagerEmail = "manager@acxiomcrm.com", ManagerPassword = "Manager@12345";
    public const string SalesEmail = "sales@acxiomcrm.com", SalesPassword = "Sales@12345";

    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in Roles.All)
            if (!await roleMgr.RoleExistsAsync(r)) await roleMgr.CreateAsync(new IdentityRole(r));

        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await EnsureUser(userMgr, AdminEmail, "System Admin", AdminPassword, Roles.Admin);
        var manager = await EnsureUser(userMgr, ManagerEmail, "Mina Manager", ManagerPassword, Roles.Manager);
        var sales = await EnsureUser(userMgr, SalesEmail, "Sam Sales", SalesPassword, Roles.SalesExecutive);

        if (await db.Customers.AnyAsync()) return;

        var c1 = new Customer { CustomerCode = "CUST-00001", CustomerName = "Ravi Kumar", Email = "ravi@globex.in", Phone = "9876543210", CompanyName = "Globex India", City = "Vijayawada", State = "Andhra Pradesh", Status = CustomerStatus.Active, CreatedBy = admin.Id, AssignedTo = sales.Id };
        var c2 = new Customer { CustomerCode = "CUST-00002", CustomerName = "Anita Rao", Email = "anita@initech.in", Phone = "9123456780", CompanyName = "Initech", City = "Hyderabad", State = "Telangana", Status = CustomerStatus.Prospect, CreatedBy = manager.Id, AssignedTo = sales.Id };
        db.Customers.AddRange(c1, c2);
        db.Leads.AddRange(
            new Lead { LeadCode = "LEAD-00001", LeadName = "Kiran Patel", Email = "kiran@umbrella.in", Phone = "9000011111", CompanyName = "Umbrella", Source = "Website", Status = LeadStatus.New, Priority = LeadPriority.High, ExpectedValue = 250000, AssignedTo = sales.Id },
            new Lead { LeadCode = "LEAD-00002", LeadName = "Meera Nair", Email = "meera@hooli.in", Phone = "9000022222", CompanyName = "Hooli", Source = "Referral", Status = LeadStatus.Qualified, Priority = LeadPriority.Medium, ExpectedValue = 500000, AssignedTo = sales.Id });
        await db.SaveChangesAsync();

        var o1 = new Opportunity { OpportunityName = "Globex CRM Rollout", CustomerId = c1.CustomerId, Amount = 750000, Stage = OpportunityStage.Proposal, Probability = 50, ExpectedCloseDate = DateTime.Today.AddDays(30), Source = "Referral", AssignedTo = sales.Id };
        var o2 = new Opportunity { OpportunityName = "Initech Support Contract", CustomerId = c2.CustomerId, Amount = 120000, Stage = OpportunityStage.Negotiation, Probability = 70, ExpectedCloseDate = DateTime.Today.AddDays(14), AssignedTo = sales.Id };
        var o3 = new Opportunity { OpportunityName = "Globex Training", CustomerId = c1.CustomerId, Amount = 90000, Stage = OpportunityStage.Won, ExpectedCloseDate = DateTime.Today.AddDays(-5), AssignedTo = sales.Id };
        o3.ApplyStageRules(); o3.ClosedDate = DateTime.Now.AddDays(-5);
        db.Opportunities.AddRange(o1, o2, o3);
        db.FollowUps.Add(new FollowUp { CustomerId = c1.CustomerId, Subject = "Proposal review call", FollowUpDate = DateTime.Today.AddDays(2), FollowUpType = ActivityType.Call, AssignedTo = sales.Id });
        db.Activities.Add(new Activity { ActivityType = ActivityType.Meeting, Subject = "Kick-off meeting", ActivityDate = DateTime.Today, CustomerId = c1.CustomerId, AssignedTo = sales.Id });
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUser(UserManager<ApplicationUser> um, string email, string name, string pwd, string role)
    {
        var u = await um.FindByEmailAsync(email);
        if (u != null) return u;
        u = new ApplicationUser { UserName = email, Email = email, FullName = name, EmailConfirmed = true, IsActive = true };
        var res = await um.CreateAsync(u, pwd);
        if (!res.Succeeded) throw new InvalidOperationException("Seed user failed: " + string.Join("; ", res.Errors.Select(e => e.Description)));
        await um.AddToRoleAsync(u, role);
        return u;
    }
}
