using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Validation;

namespace AcxiomCRM.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [StringLength(ValidationRules.EmailLength)]
    [RegularExpression(ValidationRules.EmailPattern, ErrorMessage = ValidationRules.EmailMessage)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [StringLength(128)]
    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Display(Name = "Full Name")]
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between {2} and {1} characters.")]
    public string? FullName { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(ValidationRules.EmailLength)]
    [RegularExpression(ValidationRules.EmailPattern, ErrorMessage = ValidationRules.EmailMessage)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least {2} characters.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "Confirm Password")]
    [Required(ErrorMessage = "Confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }
}

public class ChangePasswordViewModel
{
    [Display(Name = "Current Password")]
    [Required(ErrorMessage = "Current Password is required.")]
    [DataType(DataType.Password)]
    public string? CurrentPassword { get; set; }

    [Display(Name = "New Password")]
    [Required(ErrorMessage = "New Password is required.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least {2} characters.")]
    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Display(Name = "Confirm New Password")]
    [Required(ErrorMessage = "Confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }
}
