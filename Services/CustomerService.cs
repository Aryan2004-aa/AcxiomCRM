using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class CustomerService
{
    private readonly ApplicationDbContext _db;
    public CustomerService(ApplicationDbContext db) { _db = db; }

    public static void Normalize(Customer c)
    {
        c.CustomerName = c.CustomerName?.Trim();
        c.Email = c.Email?.Trim().ToLowerInvariant();
        c.Phone = c.Phone?.Trim();
        c.CompanyName = c.CompanyName?.Trim();
    }

    // Email uniqueness, phone uniqueness and duplicate-customer prevention. Returns field -> message.
    public async Task<Dictionary<string, string>> DuplicatesAsync(Customer c, int ignoreId)
    {
        var r = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(c.Email) && await _db.Customers.AnyAsync(x => x.CustomerId != ignoreId && x.Email == c.Email))
            r["Email"] = "A customer with this email already exists.";
        if (!string.IsNullOrEmpty(c.Phone) && await _db.Customers.AnyAsync(x => x.CustomerId != ignoreId && x.Phone == c.Phone))
            r["Phone"] = "A customer with this phone number already exists.";
        var name = (c.CustomerName ?? "").ToLower();
        var comp = (c.CompanyName ?? "").ToLower();
        if (name != "" && await _db.Customers.AnyAsync(x => x.CustomerId != ignoreId && x.CustomerName.ToLower() == name && (x.CompanyName ?? "").ToLower() == comp))
            r["CustomerName"] = "Duplicate customer: the same name and company already exist.";
        return r;
    }

    public async Task CreateAsync(Customer c, string userId)
    {
        c.CreatedBy = userId;
        c.CreatedDate = DateTime.Now;
        c.IsActive = true;
        c.CustomerCode = Guid.NewGuid().ToString("N")[..20];   // temporary, replaced once the Id is known
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        c.CustomerCode = $"CUST-{c.CustomerId:D5}";
        await _db.SaveChangesAsync();
    }
}
