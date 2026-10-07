using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Customer
{
    public int CustomerId { get; set; }
    [StringLength(20)] public string CustomerCode { get; set; }

    [Required(ErrorMessage = "Customer Name is required."), StringLength(100, ErrorMessage = "Customer Name cannot exceed 100 characters.")]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; }

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = Rules.EmailMessage), StringLength(150)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Phone is required."), RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)]
    public string Phone { get; set; }

    [StringLength(100)] [Display(Name = "Company")] public string CompanyName { get; set; }
    [StringLength(250)] public string Address { get; set; }
    [StringLength(60)] public string City { get; set; }
    [StringLength(60)] public string State { get; set; }

    [EnumDataType(typeof(CustomerStatus))]
    public CustomerStatus Status { get; set; } = CustomerStatus.Prospect;

    [StringLength(1000)] public string Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? ModifiedDate { get; set; }
    public string CreatedBy { get; set; }

    [Required(ErrorMessage = "Select an assigned user.")]
    [Display(Name = "Assigned To")]
    public string AssignedTo { get; set; }
    public ApplicationUser AssignedUser { get; set; }

    public bool IsActive { get; set; } = true;   // false = soft-deleted (deactivated)
}
