using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Activity
{
    public int ActivityId { get; set; }

    [EnumDataType(typeof(ActivityType))]
    [Display(Name = "Type")]
    public ActivityType ActivityType { get; set; } = ActivityType.Call;

    [Required(ErrorMessage = "Subject is required."), StringLength(150)]
    public string Subject { get; set; }

    [StringLength(1000)] public string Description { get; set; }

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Activity Date")]
    public DateTime ActivityDate { get; set; } = DateTime.Today;

    [Display(Name = "Customer")] public int? CustomerId { get; set; }
    public Customer Customer { get; set; }
    [Display(Name = "Lead")] public int? LeadId { get; set; }
    public Lead Lead { get; set; }

    [Required(ErrorMessage = "Select an assigned user.")]
    [Display(Name = "Assigned To")]
    public string AssignedTo { get; set; }
    public ApplicationUser AssignedUser { get; set; }

    [EnumDataType(typeof(ActivityStatus))]
    public ActivityStatus Status { get; set; } = ActivityStatus.Open;
}
