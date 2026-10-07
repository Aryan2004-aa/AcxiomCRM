using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Lead : IValidatableObject
{
    public static readonly string[] Sources = { "Website", "Referral", "Cold Call", "Email Campaign", "Social Media", "Event", "Other" };

    public int LeadId { get; set; }
    [StringLength(20)] public string LeadCode { get; set; }

    [Required(ErrorMessage = "Lead name is required."), StringLength(100)]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; }

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = Rules.EmailMessage), StringLength(150)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Phone is required."), RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)]
    public string Phone { get; set; }

    [StringLength(100)] [Display(Name = "Company")] public string CompanyName { get; set; }

    [Required(ErrorMessage = "Lead source is required."), StringLength(60)]
    public string Source { get; set; }

    [EnumDataType(typeof(LeadStatus))]
    public LeadStatus Status { get; set; } = LeadStatus.New;

    [EnumDataType(typeof(LeadPriority))]
    public LeadPriority Priority { get; set; } = LeadPriority.Medium;

    [Range(typeof(decimal), "0", "100000000", ErrorMessage = "Expected value must be between 0 and 100,000,000.")]
    [Display(Name = "Expected Value")]
    public decimal ExpectedValue { get; set; }

    [StringLength(1000)] public string Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Select an assigned user.")]
    [Display(Name = "Assigned To")]
    public string AssignedTo { get; set; }
    public ApplicationUser AssignedUser { get; set; }

    public int? ConvertedCustomerId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!string.IsNullOrEmpty(Source) && !Sources.Contains(Source))
            yield return new ValidationResult("Select a valid lead source.", new[] { nameof(Source) });
    }
}
