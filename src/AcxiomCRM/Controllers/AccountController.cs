using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AcxiomCRM.Controllers;

public class AccountController : CrmController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AuditService _audit;
    private readonly SecuritySettings _security;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        AuditService audit, IOptions<SecuritySettings> security)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _audit = audit;
        _security = security.Value;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null, string? reason = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        if (reason == "session")
        {
            Warning("Your session ended because your account changed. Please sign in again.");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(ApiPolicies.LoginRateLimit)]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email!.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            await _audit.LogAsync(AuditActions.LoginFailed, AuditEntities.Account, null, success: false,
                details: "Unknown account.", userName: email);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password!, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            user.LastLoginDate = DateTime.Now;
            await _userManager.UpdateAsync(user);
            await _audit.LogAsync(AuditActions.Login, AuditEntities.Account, user.Id, details: "Signed in (web).",
                userId: user.Id, userName: user.Email);
            return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            var until = user.LockoutEnd?.ToLocalTime();
            await _audit.LogAsync(AuditActions.Lockout, AuditEntities.Account, user.Id, success: false,
                details: until.HasValue ? $"Account locked until {until:dd MMM yyyy HH:mm}." : "Account locked.",
                userId: user.Id, userName: user.Email);
            ModelState.AddModelError(string.Empty,
                $"This account is locked after repeated failed sign-in attempts. Try again in {_security.Lockout.LockoutMinutes} minutes or contact an administrator.");
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            // Only reveal the inactive state to someone who knows the password.
            var passwordOk = await _userManager.CheckPasswordAsync(user, model.Password!);
            await _audit.LogAsync(AuditActions.LoginFailed, AuditEntities.Account, user.Id, success: false,
                details: passwordOk ? "Inactive account." : "Invalid password (inactive account).", userId: user.Id, userName: user.Email);
            ModelState.AddModelError(string.Empty, passwordOk
                ? "Your account is inactive. Contact your administrator."
                : "Invalid email or password.");
            return View(model);
        }

        var remaining = Math.Max(0, _security.Lockout.MaxFailedAttempts - await _userManager.GetAccessFailedCountAsync(user));
        await _audit.LogAsync(AuditActions.LoginFailed, AuditEntities.Account, user.Id, success: false,
            details: $"Invalid password. Failed attempts: {await _userManager.GetAccessFailedCountAsync(user)}.",
            userId: user.Id, userName: user.Email);
        ModelState.AddModelError(string.Empty, remaining is > 0 and <= 2
            ? $"Invalid email or password. {remaining} attempt(s) left before the account is locked."
            : "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new RegisterViewModel());
    }

    /// <summary>Self-registration creates a Sales Executive account (least privilege). Admins can change roles later.</summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(ApiPolicies.LoginRateLimit)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email!.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = model.FullName!.Trim(),
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        var result = await _userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var field = error.Code.Contains("Email") || error.Code.Contains("UserName") ? nameof(model.Email) : nameof(model.Password);
                ModelState.AddModelError(field, error.Code is "DuplicateUserName" or "DuplicateEmail"
                    ? "An account with this email already exists."
                    : error.Description);
            }

            return View(model);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.SalesExecutive);
        await _audit.LogAsync(AuditActions.Register, AuditEntities.Account, user.Id,
            newValue: new { user.FullName, user.Email, Role = AppRoles.SalesExecutive }, userId: user.Id, userName: user.Email);

        await _signInManager.SignInAsync(user, isPersistent: false);
        user.LastLoginDate = DateTime.Now;
        await _userManager.UpdateAsync(user);
        await _audit.LogAsync(AuditActions.Login, AuditEntities.Account, user.Id, details: "Signed in after registration.",
            userId: user.Id, userName: user.Email);
        Success("Welcome to AcxiomCRM. Your account has been created.");
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        var name = User.Identity?.Name;
        await _signInManager.SignOutAsync();
        await _audit.LogAsync(AuditActions.Logout, AuditEntities.Account, userId, details: "Signed out (web).", userId: userId, userName: name);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> AccessDenied(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            await _audit.LogAsync(AuditActions.AccessDenied, AuditEntities.Account, null, success: false,
                details: $"Denied: {Truncate(returnUrl, 200)}");
        }

        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword!, model.NewPassword!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code == "PasswordMismatch" ? nameof(model.CurrentPassword) : nameof(model.NewPassword),
                    error.Code == "PasswordMismatch" ? "Current password is incorrect." : error.Description);
            }

            await _audit.LogAsync(AuditActions.PasswordChange, AuditEntities.Account, user.Id, success: false, details: "Password change rejected.");
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        await _audit.LogAsync(AuditActions.PasswordChange, AuditEntities.Account, user.Id, details: "Password changed by user.");
        Success("Your password has been changed.");
        return RedirectToAction("Index", "Dashboard");
    }

    private static string? Truncate(string? value, int max) => value == null || value.Length <= max ? value : value[..max];
}
