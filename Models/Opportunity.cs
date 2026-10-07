using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Opportunity
{
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity Name is required."), StringLength(150)]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a customer.")]
    [Display(Name = "Customer")]
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }

    [Display(Name = "Lead")] public int? LeadId { get; set; }
    public Lead Lead { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [EnumDataType(typeof(OpportunityStage))]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 20;

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [NotPastDate("Expected Close Date cannot be in the past.", DependsOn = nameof(Stage), ExemptValues = "Won,Lost")]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

    [StringLength(20)] public string Status { get; set; } = "Open";   // Open / Won / Lost (derived from Stage)
    [StringLength(60)] public string Source { get; set; }
    [StringLength(1000)] public string Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ClosedDate { get; set; }

    [Required(ErrorMessage = "Select an owner.")]
    [Display(Name = "Owner")]
    public string AssignedTo { get; set; }
    public ApplicationUser AssignedUser { get; set; }

    [NotMapped] public decimal Weighted => Amount * Probability / 100m;   // Amount x Probability / 100
    [NotMapped] public bool IsActiveStage => Stage != OpportunityStage.Won && Stage != OpportunityStage.Lost;

    public void ApplyStageRules()
    {
        Status = Stage == OpportunityStage.Won ? "Won" : Stage == OpportunityStage.Lost ? "Lost" : "Open";
        if (Stage == OpportunityStage.Won) Probability = 100;
        if (Stage == OpportunityStage.Lost) Probability = 0;
        if (IsActiveStage) ClosedDate = null; else ClosedDate ??= DateTime.Now;
    }
}
