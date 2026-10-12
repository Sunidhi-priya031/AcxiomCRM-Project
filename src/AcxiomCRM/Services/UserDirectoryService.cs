using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public record UserOption(string Id, string FullName, string Role, bool IsActive);

/// <summary>
/// Looks up users and decides who a record may be assigned to:
/// Admin - any active user; Manager - self or an active team member; Sales Executive - self only.
/// </summary>
public class UserDirectoryService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _user;

    public UserDirectoryService(ApplicationDbContext db, IUserContext user)
    {
        _db = db;
        _user = user;
    }

    /// <summary>All users with their primary role.</summary>
    public async Task<List<UserOption>> GetAllUsersAsync()
    {
        var users = await _db.Users.Select(u => new { u.Id, u.FullName, u.IsActive }).ToListAsync();
        var roles = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           select new { ur.UserId, r.Name }).ToListAsync();
        var roleMap = roles.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
        return users
            .Select(u => new UserOption(u.Id, u.FullName, roleMap.GetValueOrDefault(u.Id, string.Empty), u.IsActive))
            .OrderBy(u => u.FullName)
            .ToList();
    }

    /// <summary>Users visible to the caller (used for owner filters on lists and reports).</summary>
    public async Task<List<UserOption>> GetScopeUsersAsync()
    {
        var scope = await _user.GetScopeUserIdsAsync();
        var all = await GetAllUsersAsync();
        return scope == null ? all : all.Where(u => scope.Contains(u.Id)).ToList();
    }

    /// <summary>Users the caller may assign records to.</summary>
    public async Task<List<UserOption>> GetAssignableUsersAsync()
    {
        var scopeUsers = await GetScopeUsersAsync();
        return scopeUsers.Where(u => u.IsActive).ToList();
    }

    /// <summary>
    /// Resolves the requested assignee. Empty means "assign to me". Returns null with an error message when
    /// the caller is not allowed to assign to the requested user.
    /// </summary>
    public async Task<(string? UserId, string? Error)> ResolveAssigneeAsync(string? requested, string? currentAssignee = null)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return (currentAssignee ?? _user.UserId, null);
        }

        // Keeping an existing assignment unchanged is always allowed for a record the caller can edit.
        if (requested == currentAssignee)
        {
            return (requested, null);
        }

        var assignable = await GetAssignableUsersAsync();
        return assignable.Any(u => u.Id == requested)
            ? (requested, null)
            : (null, "You cannot assign records to the selected user.");
    }

    public async Task<string?> GetPrimaryRoleAsync(string userId)
    {
        return await (from ur in _db.UserRoles
                      join r in _db.Roles on ur.RoleId equals r.Id
                      where ur.UserId == userId
                      select r.Name).FirstOrDefaultAsync();
    }
}
