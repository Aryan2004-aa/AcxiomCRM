using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public static class LeadWorkflow
{
    private static readonly Dictionary<LeadStatus, LeadStatus[]> Map = new()
    {
        { LeadStatus.New,         new[] { LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost } },
        { LeadStatus.Contacted,   new[] { LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost } },
        { LeadStatus.Qualified,   new[] { LeadStatus.Contacted, LeadStatus.Converted, LeadStatus.Lost } },
        { LeadStatus.Unqualified, new[] { LeadStatus.Contacted, LeadStatus.Lost } },
        { LeadStatus.Converted,   new LeadStatus[0] },
        { LeadStatus.Lost,        new[] { LeadStatus.New } }
    };

    public static bool IsAllowed(LeadStatus from, LeadStatus to) => from == to || Map[from].Contains(to);

    // Statuses selectable on the edit form. "Converted" is reachable only through the Convert action.
    public static IEnumerable<LeadStatus> Options(LeadStatus current) =>
        new[] { current }.Concat(Map[current].Where(s => s != LeadStatus.Converted)).Distinct();
}

public class LeadService
{
    private readonly ApplicationDbContext _db;
    private readonly CustomerService _customers;
    public LeadService(ApplicationDbContext db, CustomerService customers) { _db = db; _customers = customers; }

    public static void Normalize(Lead l)
    {
        l.LeadName = l.LeadName?.Trim();
        l.Email = l.Email?.Trim().ToLowerInvariant();
        l.Phone = l.Phone?.Trim();
    }

    public async Task CreateAsync(Lead l)
    {
        l.Status = LeadStatus.New;
        l.CreatedDate = DateTime.Now;
        l.LeadCode = Guid.NewGuid().ToString("N")[..20];
        _db.Leads.Add(l);
        await _db.SaveChangesAsync();
        l.LeadCode = $"LEAD-{l.LeadId:D5}";
        await _db.SaveChangesAsync();
    }

    public async Task<(Customer customer, Opportunity opportunity)> ConvertAsync(Lead lead, string userId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var email = lead.Email.Trim().ToLowerInvariant();
        var phone = lead.Phone.Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email || c.Phone == phone);
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerName = lead.LeadName, Email = email, Phone = phone, CompanyName = lead.CompanyName,
                Status = CustomerStatus.Active, AssignedTo = lead.AssignedTo, Notes = lead.Notes
            };
            await _customers.CreateAsync(customer, userId);
        }

        Opportunity opp = null;
        if (lead.ExpectedValue > 0)
        {
            opp = new Opportunity
            {
                OpportunityName = $"{lead.LeadName} - Opportunity", CustomerId = customer.CustomerId, LeadId = lead.LeadId,
                Amount = lead.ExpectedValue, Stage = OpportunityStage.Qualification, Probability = 20,
                ExpectedCloseDate = DateTime.Today.AddDays(30), Source = lead.Source, AssignedTo = lead.AssignedTo
            };
            opp.ApplyStageRules();
            _db.Opportunities.Add(opp);
        }

        lead.Status = LeadStatus.Converted;
        lead.ConvertedCustomerId = customer.CustomerId;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return (customer, opp);
    }
}
