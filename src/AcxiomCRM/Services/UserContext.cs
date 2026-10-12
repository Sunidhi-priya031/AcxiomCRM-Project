using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

/// <summary>
/// Information about the signed-in user and the set of users whose records they may see:
/// Admin = everyone, Manager = self + direct reports (team), Sales Executive = self.
/// </summary>
public interface IUserContext
{
    string UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool IsManager { get; }
    bool IsSalesExecutive { get; }
    string? IpAddress { get; }

    /// <summary>User ids in the caller's data scope, or null when the caller can see all records (Admin).</summary>
    Task<List<string>?> GetScopeUserIdsAsync();

    Task<ApplicationUser?> GetUserAsync();
}

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ApplicationDbContext _db;
    private List<string>? _scope;
    private bool _scopeLoaded;
    private ApplicationUser? _user;
    private bool _userLoaded;

    public UserContext(IHttpContextAccessor accessor, ApplicationDbContext db)
    {
        _accessor = accessor;
        _db = db;
    }

    private ClaimsPrincipal Principal => _accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public string UserId => Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    public string? UserName => Principal.Identity?.Name;
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public bool IsAdmin => Principal.IsInRole(AppRoles.Admin);
    public bool IsManager => !IsAdmin && Principal.IsInRole(AppRoles.Manager);
    public bool IsSalesExecutive => !IsAdmin && !IsManager;
    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public async Task<List<string>?> GetScopeUserIdsAsync()
    {
        if (_scopeLoaded)
        {
            return _scope;
        }

        _scopeLoaded = true;
        if (IsAdmin)
        {
            _scope = null;
        }
        else if (IsManager)
        {
            var me = UserId;
            var team = await _db.Users.Where(u => u.ManagerId == me).Select(u => u.Id).ToListAsync();
            team.Add(me);
            _scope = team;
        }
        else
        {
            _scope = new List<string> { UserId };
        }

        return _scope;
    }

    public async Task<ApplicationUser?> GetUserAsync()
    {
        if (!_userLoaded)
        {
            _userLoaded = true;
            var id = UserId;
            _user = string.IsNullOrEmpty(id) ? null : await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        return _user;
    }
}
