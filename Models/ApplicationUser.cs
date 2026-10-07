using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Models;

// Passwords, hashing, lockout and roles are fully managed by ASP.NET Core Identity.
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)]
    public string FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
