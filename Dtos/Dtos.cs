using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos;

public class LoginDto
{
    [Required] public string Email { get; set; }
    [Required] public string Password { get; set; }
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Code { get; set; }

    [Required, StringLength(100)] public string Name { get; set; }
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; }
    [Required, RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)] public string Phone { get; set; }
    [StringLength(100)] public string CompanyName { get; set; }
    [StringLength(250)] public string Address { get; set; }
    [StringLength(60)] public string City { get; set; }
    [StringLength(60)] public string State { get; set; }
    [EnumDataType(typeof(CustomerStatus))] public CustomerStatus Status { get; set; }
    [StringLength(1000)] public string Notes { get; set; }
    public string AssignedTo { get; set; }

    public static CustomerDto From(Customer c) => new()
    {
        Id = c.CustomerId, Code = c.CustomerCode, Name = c.CustomerName, Email = c.Email, Phone = c.Phone,
        CompanyName = c.CompanyName, Address = c.Address, City = c.City, State = c.State, Status = c.Status,
        Notes = c.Notes, AssignedTo = c.AssignedTo
    };

    public void Apply(Customer c)
    {
        c.CustomerName = Name; c.Email = Email; c.Phone = Phone; c.CompanyName = CompanyName; c.Address = Address;
        c.City = City; c.State = State; c.Status = Status; c.Notes = Notes;
    }
}

public class LeadDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    [Required, StringLength(100)] public string Name { get; set; }
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; }
    [Required, RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)] public string Phone { get; set; }
    [StringLength(100)] public string CompanyName { get; set; }
    [Required, StringLength(60)] public string Source { get; set; }
    [EnumDataType(typeof(LeadStatus))] public LeadStatus Status { get; set; }
    [EnumDataType(typeof(LeadPriority))] public LeadPriority Priority { get; set; }
    [Range(typeof(decimal), "0", "100000000")] public decimal ExpectedValue { get; set; }
    [StringLength(1000)] public string Notes { get; set; }
    public string AssignedTo { get; set; }

    public static LeadDto From(Lead l) => new()
    {
        Id = l.LeadId, Code = l.LeadCode, Name = l.LeadName, Email = l.Email, Phone = l.Phone, CompanyName = l.CompanyName,
        Source = l.Source, Status = l.Status, Priority = l.Priority, ExpectedValue = l.ExpectedValue, Notes = l.Notes, AssignedTo = l.AssignedTo
    };

    public void Apply(Lead l)
    {
        l.LeadName = Name; l.Email = Email; l.Phone = Phone; l.CompanyName = CompanyName; l.Source = Source;
        l.Priority = Priority; l.ExpectedValue = ExpectedValue; l.Notes = Notes;
    }
}

public class OpportunityDto : IValidatableObject
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "CustomerId is required.")] public int CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "Opportunity Amount must be greater than 0.")] public decimal Amount { get; set; }
    [EnumDataType(typeof(OpportunityStage))] public OpportunityStage Stage { get; set; }
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")] public int Probability { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    [StringLength(60)] public string Source { get; set; }
    [StringLength(1000)] public string Notes { get; set; }
    public string AssignedTo { get; set; }
    public string Status { get; set; }

    public static OpportunityDto From(Opportunity o) => new()
    {
        Id = o.OpportunityId, Name = o.OpportunityName, CustomerId = o.CustomerId, LeadId = o.LeadId, Amount = o.Amount,
        Stage = o.Stage, Probability = o.Probability, ExpectedCloseDate = o.ExpectedCloseDate, Source = o.Source,
        Notes = o.Notes, AssignedTo = o.AssignedTo, Status = o.Status
    };

    public void Apply(Opportunity o)
    {
        o.OpportunityName = Name; o.CustomerId = CustomerId; o.LeadId = LeadId; o.Amount = Amount; o.Stage = Stage;
        o.Probability = Probability; o.ExpectedCloseDate = ExpectedCloseDate.Date; o.Source = Source; o.Notes = Notes;
        o.ApplyStageRules();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        var active = Stage != OpportunityStage.Won && Stage != OpportunityStage.Lost;
        if (active && ExpectedCloseDate.Date < DateTime.Today)
            yield return new ValidationResult("Expected Close Date cannot be in the past.", new[] { nameof(ExpectedCloseDate) });
    }
}
