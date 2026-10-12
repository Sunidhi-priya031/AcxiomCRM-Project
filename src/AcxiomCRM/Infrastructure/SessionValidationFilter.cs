using System.Security.Claims;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AcxiomCRM.Infrastructure;

/// <summary>
/// Runs on every MVC and API request from an authenticated caller (cookie or bearer token). The session is
/// rejected immediately when the account was deactivated, or when its security stamp changed (password reset,
/// role change, API logout) - so revoked access does not linger until the cookie or token expires.
/// </summary>
public class SessionValidationFilter : IAsyncAuthorizationFilter
{
    private readonly IUserContext _userContext;
    private readonly IOptions<IdentityOptions> _identityOptions;

    public SessionValidationFilter(IUserContext userContext, IOptions<IdentityOptions> identityOptions)
    {
        _userContext = userContext;
        _identityOptions = identityOptions;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(principal.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return;
        }

        var user = await _userContext.GetUserAsync();
        var stampClaim = principal.FindFirstValue(_identityOptions.Value.ClaimsIdentity.SecurityStampClaimType);
        var valid = user != null && user.IsActive && (stampClaim == null || stampClaim == user.SecurityStamp);
        if (valid)
        {
            return;
        }

        if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Your session is no longer valid. Please sign in again."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        context.Result = new RedirectToActionResult("Login", "Account", new { reason = "session" });
    }
}
