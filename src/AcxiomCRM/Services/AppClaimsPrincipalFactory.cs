using System.Security.Claims;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AcxiomCRM.Services;

/// <summary>Adds the user's display name to the identity so the layout can show it without a database call.</summary>
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string FullNameClaim = "full_name";

    public AppClaimsPrincipalFactory(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(FullNameClaim, user.FullName));
        return identity;
    }
}
