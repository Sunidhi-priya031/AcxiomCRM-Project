using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Models;

/// <summary>
/// Application user built on ASP.NET Core Identity. Password hashes, lockout counters and
/// security stamps are managed by Identity itself - no custom password storage exists.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Reporting manager. A manager's team is the set of users whose ManagerId is the manager.</summary>
    public string? ManagerId { get; set; }

    [ForeignKey(nameof(ManagerId))]
    public ApplicationUser? Manager { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? LastLoginDate { get; set; }
}
