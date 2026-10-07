using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OfficialLedger.Data;

namespace OfficialLedger.Components.Account;

public class FirstNameClaimsFactory(UserManager<ApplicationUser> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.FirstName))
            identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName.Trim()));
        return identity;
    }
}
