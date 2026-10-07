using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class LoginVm
{
    [Required(ErrorMessage = "Email or username is required.")]
    [Display(Name = "Email / Username")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required."), DataType(DataType.Password)]
    public string Password { get; set; }

    [Display(Name = "Remember me")] public bool RememberMe { get; set; }
}

public class RegisterVm
{
    [Required(ErrorMessage = "Full name is required."), StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; }

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required."), StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters."), DataType(DataType.Password)]
    public string Password { get; set; }

    [DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; }
}
