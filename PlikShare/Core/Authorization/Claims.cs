using System.Security.Claims;
using PlikShare.Core.UserIdentity;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;

namespace PlikShare.Core.Authorization;

public static class Claims
{
    public const string Email = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";
    public const string Role = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    public const string BoxLinkSessionId = "http://plikshare.com/claims/boxlinksessionid";
    public const string UserExternalId = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
    public const string UserDatabaseId = "user_database_id";
    public const string RememberMe = "remember_me";
    public const string IsAppOwner = "is_app_owner";
    public const string Permission = "permission";
    public const string SecurityStamp = "AspNet.Identity.SecurityStamp";
    public const string ConcurrencyStamp = "concurrency_stamp";
    
    public static void CopyClaimIfExists(
        this ClaimsIdentity newIdentity, 
        ClaimsIdentity currentIdentity,
        string claimType)
    {
        var claim = currentIdentity.Claims.FirstOrDefault(claim =>
            claim.Type == claimType);

        if (claim is not null)
        {
            newIdentity.AddClaim(new Claim(claim.Type, claim.Value));
        }
    }

    public static bool HasPermission(this ClaimsPrincipal claimsPrincipal, string permission)
    {
        var claims = claimsPrincipal
            .Claims
            .Where(c => string.Equals(c.Type, Permission, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        return claims.Count != 0 
               && claims.Any(c => string.Equals(c.Value, permission, StringComparison.InvariantCultureIgnoreCase));
    }
    
    public static bool GetIsAppOwner(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, IsAppOwner, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
            return false;

        if (!bool.TryParse(claim.Value, out var isAppOwner))
        {
            throw new InvalidOperationException(
                $"'IsAppOwner' value '{claim.Value}' is in wrong format.");
        }

        return isAppOwner;
    }
    
    public static bool GetRememberMeOrDefault(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, RememberMe, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
            return false;

        if (!bool.TryParse(claim.Value, out var rememberMe))
        {
            throw new InvalidOperationException(
                $"'RememberMe' value '{claim.Value}' is in wrong format.");
        }

        return rememberMe;
    }
    
    public static int GetDatabaseId(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, UserDatabaseId, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'UserDatabaseId' claim was not found.");
        }

        if (!int.TryParse(claim.Value, out var databaseId))
        {
            throw new InvalidOperationException(
                $"'UserDatabaseId' value '{claim.Value}' is in wrong format.");
        }

        return databaseId;
    }
    
    public static string GetExternalIdValue(this ClaimsIdentity claimsIdentity)
    {
        var claim = claimsIdentity
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, UserExternalId, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'UserExternalId' claim was not found.");
        }

        return claim.Value;
    }
    
    public static string GetExternalIdValue(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, UserExternalId, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'UserExternalId' claim was not found.");
        }

        return claim.Value;
    }
    
    public static UserExtId GetExternalId(this ClaimsIdentity claimsIdentity)
    {
        var claimValue = claimsIdentity.GetExternalIdValue();
        
        if (!UserExtId.TryParse(claimValue, out var externalId))
        {
            throw new InvalidOperationException(
                $"'UserExternalId' value '{claimValue}' is in wrong format.");
        }

        return externalId;
    }
    
    public static UserExtId GetExternalId(this ClaimsPrincipal claimsPrincipal)
    {
        var claimValue = claimsPrincipal.GetExternalIdValue();
        
        if (!UserExtId.TryParse(claimValue, out var externalId))
        {
            throw new InvalidOperationException(
                $"'UserExternalId' value '{claimValue}' is in wrong format.");
        }

        return externalId;
    }
    
    public static string GetEmail(this ClaimsIdentity claimsIdentity)
    {
        var claim = claimsIdentity
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, Email, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'Email' claim was not found.");
        }

        return claim.Value;
    }

    public static string GetSecurityStamp(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, SecurityStamp, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'SecurityStamp' claim was not found.");
        }

        return claim.Value;
    }
    
    public static string GetConcurrencyStamp(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, ConcurrencyStamp, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'ConcurrencyStamp' claim was not found.");
        }

        return claim.Value;
    }
    
    public static string GetEmail(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, Email, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'Email' claim was not found.");
        }

        return claim.Value;
    }
    
    public static Email? TryGetEmail(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, Email, StringComparison.InvariantCultureIgnoreCase));

        return claim is null 
            ? null 
            : new Email(claim.Value);
    }

    public static Guid GetBoxLinkSessionIdOrThrow(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, BoxLinkSessionId, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'BoxLinkSessionId' claim was not found.");
        }

        if (!Guid.TryParse(claim.Value, out var boxLinkSessionId))
        {
            throw new InvalidOperationException(
                $"'BoxLinkSessionId' claim format is not correct. Expected Guid but found '{claim.Value}'");
        }

        return boxLinkSessionId;
    }

    public static List<IUserIdentity> GetUserIdentities(this ClaimsPrincipal claimsPrincipal)
    {
        var result = new List<IUserIdentity>();

        var boxLinkSessionClaim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, BoxLinkSessionId, StringComparison.InvariantCultureIgnoreCase));
        
        if (boxLinkSessionClaim is not null)
        {
            if (!Guid.TryParse(boxLinkSessionClaim.Value, out var boxLinkSessionId))
            {
                throw new InvalidOperationException(
                    $"'BoxLinkSessionId' claim format is not correct. Expected Guid but found '{boxLinkSessionClaim.Value}'");
            }

            result.Add(new BoxLinkSessionUserIdentity(
                boxLinkSessionId));
        }
        
        var userExternalIdClaim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, UserExternalId, StringComparison.InvariantCultureIgnoreCase));

        if (userExternalIdClaim is  not null)
        {
            if (!UserExtId.TryParse(userExternalIdClaim.Value, out var externalId))
            {
                throw new InvalidOperationException(
                    $"'UserExternalId' value '{userExternalIdClaim.Value}' is in wrong format.");
            }

            result.Add(new UserIdentity.UserIdentity(
                externalId));
        }

        return result;
    }
}