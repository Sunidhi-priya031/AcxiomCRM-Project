using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

/// <summary>
/// User and role administration (Admin only for changes; Managers may view their team).
/// All password handling goes through ASP.NET Core Identity's UserManager.
/// </summary>
public class UserAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserContext _user;
    private readonly AuditService _audit;
    private readonly UserDirectoryService _directory;

    public UserAdminService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IUserContext user,
        AuditService audit, UserDirectoryService directory)
    {
        _db = db;
        _userManager = userManager;
        _user = user;
        _audit = audit;
        _directory = directory;
    }

    public async Task<PagedResult<UserListItem>> SearchAsync(UserSearch s)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var roles = await _directory.GetAllUsersAsync();
        var roleMap = roles.ToDictionary(r => r.Id, r => r.Role);
        var names = users.ToDictionary(u => u.Id, u => u.FullName);
        var now = DateTimeOffset.UtcNow;

        var items = users
            .Where(u => scope == null || scope.Contains(u.Id))
            .Select(u => ToListItem(u, roleMap.GetValueOrDefault(u.Id, ""), u.ManagerId != null ? names.GetValueOrDefault(u.ManagerId) : null, now));

        if (!string.IsNullOrWhiteSpace(s.Q))
        {
            var term = s.Q.Trim();
            items = items.Where(u => u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     u.Email.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (AppRoles.IsValid(s.Role)) items = items.Where(u => u.Role == s.Role);
        items = s.State switch
        {
            "active" => items.Where(u => u.IsActive),
            "inactive" => items.Where(u => !u.IsActive),
            "locked" => items.Where(u => u.IsLockedOut),
            _ => items
        };

        items = s.Sort switch
        {
            "name_desc" => items.OrderByDescending(u => u.FullName),
            "email" => items.OrderBy(u => u.Email),
            "role" => items.OrderBy(u => u.Role).ThenBy(u => u.FullName),
            "lastlogin" => items.OrderByDescending(u => u.LastLoginDate),
            "created" => items.OrderByDescending(u => u.CreatedDate),
            _ => items.OrderBy(u => u.FullName)
        };

        return items.ToPagedResult(s.Page, s.PageSize);
    }

    private static UserListItem ToListItem(ApplicationUser u, string role, string? managerName, DateTimeOffset now) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email ?? "",
        Role = role,
        ManagerName = managerName,
        IsActive = u.IsActive,
        IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > now,
        LockoutEnd = u.LockoutEnd,
        AccessFailedCount = u.AccessFailedCount,
        LastLoginDate = u.LastLoginDate,
        CreatedDate = u.CreatedDate
    };

    public async Task<ServiceResult<UserListItem>> GetAsync(string id)
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return ServiceResult<UserListItem>.NotFound();
        if (scope != null && !scope.Contains(id)) return ServiceResult<UserListItem>.Forbidden();

        var role = await _directory.GetPrimaryRoleAsync(id) ?? "";
        string? managerName = null;
        if (user.ManagerId != null)
        {
            managerName = await _db.Users.Where(u => u.Id == user.ManagerId).Select(u => u.FullName).FirstOrDefaultAsync();
        }

        return ServiceResult<UserListItem>.Ok(ToListItem(user, role, managerName, DateTimeOffset.UtcNow));
    }

    public async Task<List<UserOption>> GetManagersAsync() =>
        (await _directory.GetAllUsersAsync()).Where(u => u.Role == AppRoles.Manager && u.IsActive).ToList();

    private async Task ValidateCommonAsync(UserFormViewModel model, ValidationErrors errors, string? userId)
    {
        if (!AppRoles.IsValid(model.Role))
        {
            errors.Add(nameof(model.Role), "Select a valid role.");
        }

        if (!string.IsNullOrEmpty(model.ManagerId))
        {
            if (model.Role != AppRoles.SalesExecutive)
            {
                errors.Add(nameof(model.ManagerId), "Only Sales Executives report to a manager.");
            }
            else if (model.ManagerId == userId || !(await GetManagersAsync()).Any(m => m.Id == model.ManagerId))
            {
                errors.Add(nameof(model.ManagerId), "Select an active manager.");
            }
        }

        var email = model.Email!.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null && existing.Id != userId)
        {
            errors.Add(nameof(model.Email), "A user with this email already exists.");
        }
    }

    public async Task<ServiceResult<ApplicationUser>> CreateAsync(UserFormViewModel model)
    {
        var errors = new ValidationErrors();
        if (string.IsNullOrEmpty(model.Password))
        {
            errors.Add(nameof(model.Password), "Password is required.");
        }

        await ValidateCommonAsync(model, errors, null);
        if (errors.Any) return ServiceResult<ApplicationUser>.Invalid(errors.Errors);

        var email = model.Email!.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = model.FullName!.Trim(),
            IsActive = model.IsActive,
            ManagerId = string.IsNullOrEmpty(model.ManagerId) ? null : model.ManagerId,
            CreatedDate = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            return ServiceResult<ApplicationUser>.Invalid(IdentityErrors(result, nameof(model.Password)));
        }

        await _userManager.AddToRoleAsync(user, model.Role!);
        await _audit.LogAsync(AuditActions.Create, AuditEntities.User, user.Id, null,
            new { user.FullName, user.Email, Role = model.Role, user.IsActive, user.ManagerId });
        return ServiceResult<ApplicationUser>.Ok(user);
    }

    public async Task<ServiceResult<ApplicationUser>> UpdateAsync(string id, UserFormViewModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return ServiceResult<ApplicationUser>.NotFound();

        var errors = new ValidationErrors();
        await ValidateCommonAsync(model, errors, id);
        var currentRole = await _directory.GetPrimaryRoleAsync(id);
        var isSelf = id == _user.UserId;
        if (isSelf && model.Role != currentRole)
        {
            errors.Add(nameof(model.Role), "You cannot change your own role.");
        }

        if (isSelf && !model.IsActive)
        {
            errors.Add(nameof(model.IsActive), "You cannot deactivate your own account.");
        }

        if (currentRole == AppRoles.Admin && (model.Role != AppRoles.Admin || !model.IsActive) && await CountActiveAdminsAsync() <= 1)
        {
            errors.Add(nameof(model.Role), "At least one active Admin account is required.");
        }

        if (currentRole == AppRoles.Manager && model.Role != AppRoles.Manager && await _db.Users.AnyAsync(u => u.ManagerId == id))
        {
            errors.Add(nameof(model.Role), "Reassign this manager's team members before changing the role.");
        }

        if (errors.Any) return ServiceResult<ApplicationUser>.Invalid(errors.Errors);

        var before = new { user.FullName, user.Email, user.IsActive, user.ManagerId };
        var email = model.Email!.Trim();
        user.FullName = model.FullName!.Trim();
        user.Email = email;
        user.UserName = email;
        user.ManagerId = string.IsNullOrEmpty(model.ManagerId) || model.Role != AppRoles.SalesExecutive ? null : model.ManagerId;
        var activeChanged = user.IsActive != model.IsActive;
        user.IsActive = model.IsActive;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return ServiceResult<ApplicationUser>.Invalid(IdentityErrors(result, nameof(model.Email)));
        }

        var after = new { user.FullName, user.Email, user.IsActive, user.ManagerId };
        if (!Equals(before, after))
        {
            await _audit.LogAsync(AuditActions.Update, AuditEntities.User, id, before, after);
        }

        if (currentRole != model.Role)
        {
            if (currentRole != null) await _userManager.RemoveFromRoleAsync(user, currentRole);
            await _userManager.AddToRoleAsync(user, model.Role!);
            await _audit.LogAsync(AuditActions.RoleChange, AuditEntities.User, id,
                new { Role = currentRole }, new { Role = model.Role }, details: $"Role changed for {user.Email}.");
        }

        if (activeChanged)
        {
            await _audit.LogAsync(user.IsActive ? AuditActions.Activate : AuditActions.Deactivate, AuditEntities.User, id,
                new { IsActive = !user.IsActive }, new { user.IsActive });
        }

        if (activeChanged || currentRole != model.Role)
        {
            // Invalidates existing cookies/tokens so the change takes effect immediately.
            await _userManager.UpdateSecurityStampAsync(user);
        }

        return ServiceResult<ApplicationUser>.Ok(user);
    }

    public async Task<ServiceResult> SetActiveAsync(string id, bool active)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return ServiceResult.NotFound();
        if (user.IsActive == active) return ServiceResult.Ok();
        if (!active && id == _user.UserId) return ServiceResult.Invalid(string.Empty, "You cannot deactivate your own account.");
        if (!active && await _directory.GetPrimaryRoleAsync(id) == AppRoles.Admin && await CountActiveAdminsAsync() <= 1)
        {
            return ServiceResult.Invalid(string.Empty, "At least one active Admin account is required.");
        }

        user.IsActive = active;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        await _audit.LogAsync(active ? AuditActions.Activate : AuditActions.Deactivate, AuditEntities.User, id,
            new { IsActive = !active }, new { IsActive = active }, details: user.Email);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnlockAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return ServiceResult.NotFound();

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _audit.LogAsync(AuditActions.Unlock, AuditEntities.User, id, details: $"Account unlocked: {user.Email}");
        return ServiceResult.Ok();
    }

    /// <summary>Admin password reset through Identity's token flow, so the password policy is enforced.</summary>
    public async Task<ServiceResult> ResetPasswordAsync(string id, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return ServiceResult.NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return ServiceResult.Invalid(IdentityErrors(result, nameof(ResetPasswordViewModel.NewPassword)));
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        // The password itself is never logged.
        await _audit.LogAsync(AuditActions.PasswordReset, AuditEntities.User, id, details: $"Password reset by administrator for {user.Email}.");
        return ServiceResult.Ok();
    }

    public async Task<RolesViewModel> GetRolesAsync()
    {
        var users = await _directory.GetAllUsersAsync();
        var descriptions = new Dictionary<string, string>
        {
            [AppRoles.Admin] = "Full administration: users, roles, audit logs, all CRM records and reports.",
            [AppRoles.Manager] = "Team customers, leads, opportunities and follow-ups; team pipeline and management reports. No security administration.",
            [AppRoles.SalesExecutive] = "Own and assigned customers, leads, opportunities, follow-ups and activities; own sales reports."
        };

        return new RolesViewModel
        {
            Roles = AppRoles.All.Select(r => new RoleSummary(r, AppRoles.DisplayName(r), descriptions[r],
                users.Count(u => u.Role == r && u.IsActive), users.Count(u => u.Role == r))).ToList(),
            Permissions = new()
            {
                new("Dashboard", "All records", "Team", "Own / assigned"),
                new("Customers", "Full", "Team (create, edit, delete/deactivate)", "Own / assigned (create, edit)"),
                new("Leads", "Full", "Team (incl. delete, convert)", "Own / assigned (create, edit, convert)"),
                new("Opportunities", "Full", "Team (incl. delete)", "Own / assigned (create, edit, close)"),
                new("Follow-Ups", "Full", "Team", "Own / assigned"),
                new("Activities", "Full", "Team (incl. delete)", "Own / assigned (create, edit)"),
                new("User Management", "Full", "View team (read-only)", "No access"),
                new("Role Management", "Full", "No access", "No access"),
                new("Audit Log", "Full (read-only, append-only)", "No access", "No access"),
                new("Reports", "All reports", "Team reports incl. user activity", "Own sales reports"),
                new("REST API", "All endpoints, all records", "All endpoints, team records", "All endpoints, own records")
            }
        };
    }

    private async Task<int> CountActiveAdminsAsync()
    {
        var users = await _directory.GetAllUsersAsync();
        return users.Count(u => u.Role == AppRoles.Admin && u.IsActive);
    }

    private static Dictionary<string, List<string>> IdentityErrors(IdentityResult result, string field)
    {
        var errors = new ValidationErrors();
        foreach (var error in result.Errors)
        {
            var key = error.Code.Contains("Email", StringComparison.OrdinalIgnoreCase) || error.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase)
                ? "Email"
                : field;
            errors.Add(key, error.Description);
        }

        return errors.Errors;
    }
}
