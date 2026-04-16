using System.Security.Claims;
using PlikShare.Users.Cache;

namespace PlikShare.Core.Authorization;

public static class UserContextClaimsExtensions
{
    public static IEnumerable<Claim> GetClaims(this UserContext user)
    {
        yield return new Claim(Claims.ConcurrencyStampClaim, user.Stamps.Concurrency);
               
        if (user.Roles.IsAppOwner)
        {
            yield return new Claim(Claims.IsAppOwnerClaim, "true");

            //we need to return this claim only for app owner role, because admin role is stored in ur_user_roles table
            //and claim is added automatically
            yield return new Claim(Claims.RoleClaim, Roles.Admin);
        }
    }
}