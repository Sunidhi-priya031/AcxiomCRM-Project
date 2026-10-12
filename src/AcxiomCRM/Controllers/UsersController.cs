using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

/// <summary>User administration. Admin: full. Manager: read-only view of their team. Sales Executive: no access.</summary>
[Authorize(Roles = AppRoles.AdminOrManager)]
public class UsersController : CrmController
{
    private readonly UserAdminService _admin;
    private readonly UserDirectoryService _directory;
    private readonly IUserContext _user;
    private readonly ApplicationDbContext _db;

    public UsersController(UserAdminService admin, UserDirectoryService directory, IUserContext user, ApplicationDbContext db)
    {
        _admin = admin;
        _directory = directory;
        _user = user;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] UserSearch search)
    {
        return View(new UserIndexViewModel
        {
            Search = search,
            Users = await _admin.SearchAsync(search),
            CanManage = _user.IsAdmin
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var result = await _admin.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var teamIds = await _db.Users.Where(x => x.ManagerId == id).Select(x => x.Id).ToListAsync();
        var model = new UserDetailsViewModel
        {
            User = result.Value!,
            CanManage = _user.IsAdmin,
            TeamMembers = (await _directory.GetAllUsersAsync()).Where(u => teamIds.Contains(u.Id)).ToList()
        };

        if (_user.IsAdmin)
        {
            model.RecentEvents = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.UserId == id || (a.EntityName == AuditEntities.User && a.RecordId == id))
                .OrderByDescending(a => a.AuditLogId).Take(20).ToListAsync();
        }

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Create()
    {
        return View(new UserFormViewModel { Role = AppRoles.SalesExecutive, IsActive = true, Managers = await _admin.GetManagersAsync() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        }

        if (ModelState.IsValid)
        {
            var result = await _admin.CreateAsync(model);
            if (result.Succeeded)
            {
                Success($"User {result.Value!.Email} created.");
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            result.CopyTo(ModelState);
        }

        model.Password = model.ConfirmPassword = null;
        model.Managers = await _admin.GetManagersAsync();
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Edit(string id)
    {
        var result = await _admin.GetAsync(id);
        if (Denied(result) is { } denied) return denied;

        var u = result.Value!;
        var user = await _db.Users.AsNoTracking().FirstAsync(x => x.Id == id);
        return View(new UserFormViewModel
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Role = u.Role,
            ManagerId = user.ManagerId,
            IsActive = u.IsActive,
            IsLockedOut = u.IsLockedOut,
            Managers = await _admin.GetManagersAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Edit(string id, UserFormViewModel model)
    {
        // Passwords are changed through Reset Password, never through this form.
        ModelState.Remove(nameof(model.Password));
        ModelState.Remove(nameof(model.ConfirmPassword));
        if (ModelState.IsValid)
        {
            var result = await _admin.UpdateAsync(id, model);
            if (Denied(result) is { } denied) return denied;
            if (result.Succeeded)
            {
                Success("User updated.");
                return RedirectToAction(nameof(Details), new { id });
            }

            result.CopyTo(ModelState);
        }

        model.Id = id;
        model.Managers = await _admin.GetManagersAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> SetActive(string id, bool active)
    {
        var result = await _admin.SetActiveAsync(id, active);
        if (Denied(result) is { } denied) return denied;
        if (result.Succeeded) Success(active ? "User activated." : "User deactivated. Their sessions have been ended.");
        else Error(result.FirstError ?? "The user could not be updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Unlock(string id)
    {
        var result = await _admin.UnlockAsync(id);
        if (Denied(result) is { } denied) return denied;
        Success("Account unlocked.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var result = await _admin.GetAsync(id);
        if (Denied(result) is { } denied) return denied;
        return View(new ResetPasswordViewModel { Id = id, FullName = result.Value!.FullName, Email = result.Value.Email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> ResetPassword(string id, ResetPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _admin.ResetPasswordAsync(id, model.NewPassword!);
            if (Denied(result) is { } denied) return denied;
            if (result.Succeeded)
            {
                Success("Password reset. Share the new password with the user through a secure channel.");
                return RedirectToAction(nameof(Details), new { id });
            }

            result.CopyTo(ModelState);
        }

        var user = await _admin.GetAsync(id);
        model.Id = id;
        model.FullName = user.Value?.FullName;
        model.Email = user.Value?.Email;
        model.NewPassword = model.ConfirmPassword = null;
        return View(model);
    }
}
