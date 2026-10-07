using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class FollowUp : IValidatableObject
{
    public int FollowUpId { get; set; }

    [Display(Name = "Customer")] public int? CustomerId { get; set; }
    public Customer Customer { get; set; }
    [Display(Name = "Lead")] public int? LeadId { get; set; }
    public Lead Lead { get; set; }
    [Display(Name = "Opportunity")] public int? OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; }

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [NotPastDate("Follow-up date cannot be earlier than today.", DependsOn = nameof(Status), ExemptValues = "Completed,Missed,Cancelled")]
    [Display(Name = "Follow-Up Date")]
    public DateTime FollowUpDate { get; set; } = DateTime.Today.AddDays(1);

    [Required(ErrorMessage = "Subject is required."), StringLength(150)]
    public string Subject { get; set; }

    [EnumDataType(typeof(ActivityType))]
    [Display(Name = "Type")]
    public ActivityType FollowUpType { get; set; } = ActivityType.Call;

    [StringLength(1000)] public string Remarks { get; set; }

    [EnumDataType(typeof(FollowUpStatus))]
    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    [Required(ErrorMessage = "Select an assigned user.")]
    [Display(Name = "Assigned To")]
    public string AssignedTo { get; set; }
    public ApplicationUser AssignedUser { get; set; }

    public bool IsOverdue => Status == FollowUpStatus.Planned && FollowUpDate.Date < DateTime.Today;

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (CustomerId == null && LeadId == null && OpportunityId == null)
            yield return new ValidationResult("Select a customer, lead or opportunity for this follow-up.");
    }
}
