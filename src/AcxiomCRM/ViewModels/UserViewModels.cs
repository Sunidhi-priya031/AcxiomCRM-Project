using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Services;
using AcxiomCRM.Validation;

namespace AcxiomCRM.ViewModels;

public class UserListItem
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class UserSearch
{
    public string? Q { get; set; }
    public string? Role { get; set; }

    /// <summary>"active", "inactive" or "locked".</summary>
    public string? State { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public class UserIndexViewModel
{
    public UserSearch Search { get; set; } = new();
    public PagedResult<UserListItem> Users { get; set; } = new();
    public bool CanManage { get; set; }
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Display(Name = "Full Name")]
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between {2} and {1} characters.")]
    public string? FullName { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(ValidationRules.EmailLength)]
    [RegularExpression(ValidationRules.EmailPattern, ErrorMessage = ValidationRules.EmailMessage)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Role is required.")]
    public string? Role { get; set; }

    [Display(Name = "Reports To (Manager)")]
    public string? ManagerId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Only used when creating a user.</summary>
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least {2} characters.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "Confirm Password")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    public List<UserOption> Managers { get; set; } = new();
    public bool IsLockedOut { get; set; }
}

public class ResetPasswordViewModel
{
    public string Id { get; set; } = "";
    public string? FullName { get; set; }
    public string? Email { get; set; }

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

public class UserDetailsViewModel
{
    public UserListItem User { get; set; } = new();
    public List<Models.AuditLog> RecentEvents { get; set; } = new();
    public List<UserOption> TeamMembers { get; set; } = new();
    public bool CanManage { get; set; }
}

public class RolesViewModel
{
    public List<RoleSummary> Roles { get; set; } = new();
    public List<PermissionRow> Permissions { get; set; } = new();
}

public record RoleSummary(string Name, string DisplayName, string Description, int ActiveUsers, int TotalUsers);

public record PermissionRow(string Module, string Admin, string Manager, string SalesExecutive);
