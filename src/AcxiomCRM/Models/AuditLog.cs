using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

/// <summary>
/// Append-only audit record. No UI or API updates/deletes it; the DbContext rejects
/// modifications and the database has triggers that block UPDATE/DELETE.
/// UserId is intentionally not a foreign key so audit history survives any user change.
/// </summary>
public class AuditLog
{
    [Key]
    public long AuditLogId { get; set; }

    [StringLength(450)]
    public string? UserId { get; set; }

    /// <summary>Snapshot of the user name/email at the time of the event (also used for failed logins).</summary>
    [StringLength(256)]
    public string? UserName { get; set; }

    [Required, StringLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Module / entity, e.g. Customer, Lead, Account, User.</summary>
    [Required, StringLength(50)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(64)]
    public string? RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    /// <summary>Success or Failure.</summary>
    [Required, StringLength(20)]
    public string Result { get; set; } = AuditResults.Success;

    [StringLength(500)]
    public string? Details { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    [StringLength(45)]
    public string? IpAddress { get; set; }
}

public static class AuditResults
{
    public const string Success = "Success";
    public const string Failure = "Failure";
}

public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Lockout = "Lockout";
    public const string Logout = "Logout";
    public const string Register = "Register";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Deactivate = "Deactivate";
    public const string Activate = "Activate";
    public const string StatusChange = "StatusChange";
    public const string Convert = "Convert";
    public const string Complete = "Complete";
    public const string Reschedule = "Reschedule";
    public const string RoleChange = "RoleChange";
    public const string PasswordChange = "PasswordChange";
    public const string PasswordReset = "PasswordReset";
    public const string Unlock = "Unlock";
    public const string AccessDenied = "AccessDenied";

    public static readonly string[] All =
    {
        Login, LoginFailed, Lockout, Logout, Register, Create, Update, Delete, Deactivate, Activate,
        StatusChange, Convert, Complete, Reschedule, RoleChange, PasswordChange, PasswordReset, Unlock, AccessDenied
    };
}

public static class AuditEntities
{
    public const string Account = "Account";
    public const string User = "User";
    public const string Customer = "Customer";
    public const string Lead = "Lead";
    public const string Opportunity = "Opportunity";
    public const string FollowUp = "FollowUp";
    public const string Activity = "Activity";

    public static readonly string[] All = { Account, User, Customer, Lead, Opportunity, FollowUp, Activity };
}
