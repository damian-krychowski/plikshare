using System.Security.Claims;
using PlikShare.Users.Cache;

namespace PlikShare.Core.Authorization;

public static class UserContextClaimsExtensions
{
    public static IEnumerable<Claim> GetClaims(this UserContext user)
    {
        yield return new Claim(Claims.UserDatabaseId, user.Id.ToString());
        yield return new Claim(Claims.ConcurrencyStamp, user.Stamps.Concurrency);
        
        if (user.Roles.IsAppOwner)
        {
            yield return new Claim(Claims.IsAppOwner, true.ToString());
            yield return new Claim(Claims.Role, Roles.Admin);
        }
    }
}