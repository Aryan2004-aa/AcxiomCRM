using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class UserListItem
{
    public string Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
}

public class UserFormVm
{
    public string Id { get; set; }

    [Required(ErrorMessage = "Full name is required."), StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; }

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; }

    [Display(Name = "Active")] public bool IsActive { get; set; } = true;

    [DataType(DataType.Password)] public string Password { get; set; }
}

public class ResetPasswordVm
{
    public string Id { get; set; }
    public string Email { get; set; }

    [Required(ErrorMessage = "New password is required."), DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; }
}
