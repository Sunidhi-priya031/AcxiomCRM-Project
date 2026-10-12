using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers.Api;

[Route("api/auth")]
public class AuthApiController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AuditService _audit;

    public AuthApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, AuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _audit = audit;
    }

    /// <summary>
    /// Authenticates with email and password and returns a bearer token:
    /// { "tokenType": "Bearer", "accessToken": "...", "expiresIn": 3600, "refreshToken": "..." }.
    /// Applies the same lockout policy as the web login and is rate limited per IP.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiPolicies.LoginRateLimit)]
    public async Task<IActionResult> Login(ApiLoginRequest request)
    {
        var email = request.Email!.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            await _audit.LogAsync(AuditActions.LoginFailed, AuditEntities.Account, null, success: false, details: "Unknown account (API).", userName: email);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid email or password." });
        }

        // The bearer handler writes the token response when sign-in succeeds.
        _signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        var result = await _signInManager.PasswordSignInAsync(user, request.Password!, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            user.LastLoginDate = DateTime.Now;
            await _userManager.UpdateAsync(user);
            await _audit.LogAsync(AuditActions.Login, AuditEntities.Account, user.Id, details: "Signed in (API token).", userId: user.Id, userName: user.Email);
            return new EmptyResult();
        }

        if (result.IsLockedOut)
        {
            await _audit.LogAsync(AuditActions.Lockout, AuditEntities.Account, user.Id, success: false, details: "Account locked (API).", userId: user.Id, userName: user.Email);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "This account is locked after repeated failed sign-in attempts." });
        }

        await _audit.LogAsync(AuditActions.LoginFailed, AuditEntities.Account, user.Id, success: false,
            details: result.IsNotAllowed ? "Inactive account (API)." : "Invalid password (API).", userId: user.Id, userName: user.Email);
        return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid email or password." });
    }

    /// <summary>
    /// Ends the caller's sessions: the security stamp is rotated, which invalidates every issued bearer token
    /// and cookie for this user on their next request.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        await _userManager.UpdateSecurityStampAsync(user);
        await _signInManager.SignOutAsync();
        await _audit.LogAsync(AuditActions.Logout, AuditEntities.Account, user.Id, details: "Signed out (API).", userId: user.Id, userName: user.Email);
        return Ok(new { message = "Signed out." });
    }
}
