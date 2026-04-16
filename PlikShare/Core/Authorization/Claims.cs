using System.Security.Claims;
using PlikShare.Core.UserIdentity;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;

namespace PlikShare.Core.Authorization;

public static class Claims
{
    public const string EmailClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

    //role claims are controlled internally by microsoft.identity and are stored inside ur_user_roles
    public const string RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    public const string BoxLinkSessionIdClaim = "http://plikshare.com/claims/boxlinksessionid";
    public const string UserExternalIdClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
    public const string RememberMeClaim = "remember_me";
    public const string IsAppOwnerClaim = "is_app_owner";
    public const string SecurityStampClaim = "AspNet.Identity.SecurityStamp";
    public const string ConcurrencyStampClaim = "concurrency_stamp";

    //permission claims are controlled internally by microsoft.identity and are stored inside uc_user_claims table
    public const string Permission = "permission";

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
    
    public static bool IsAppOwner(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, IsAppOwnerClaim, StringComparison.InvariantCultureIgnoreCase));

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
                string.Equals(c.Type, RememberMeClaim, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
            return false;

        if (!bool.TryParse(claim.Value, out var rememberMe))
        {
            throw new InvalidOperationException(
                $"'RememberMe' value '{claim.Value}' is in wrong format.");
        }

        return rememberMe;
    }
    
    public static UserExtId GetExternalId(this ClaimsIdentity claimsIdentity)
    {
        var claim = claimsIdentity
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, UserExternalIdClaim, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                "'UserExternalId' claim was not found.");
        }

        if (!UserExtId.TryParse(claim.Value, out var externalId))
        {
            throw new InvalidOperationException(
                $"'UserExternalId' value '{claim.Value}' is in wrong format.");
        }

        return externalId;
    }
    
    public static UserExtId GetExternalId(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, UserExternalIdClaim, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'UserExternalId' claim was not found.");
        }
        
        if (!UserExtId.TryParse(claim.Value, out var externalId))
        {
            throw new InvalidOperationException(
                $"'UserExternalId' value '{claim.Value}' is in wrong format.");
        }

        return externalId;
    }
    
    public static string GetSecurityStamp(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, SecurityStampClaim, StringComparison.InvariantCultureIgnoreCase));

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
                string.Equals(c.Type, ConcurrencyStampClaim, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
        {
            throw new InvalidOperationException(
                $"'ConcurrencyStamp' claim was not found.");
        }

        return claim.Value;
    }
    
    public static Email GetEmail(this ClaimsPrincipal claimsPrincipal)
    {
        var email = claimsPrincipal.TryGetEmail();
        
        if (email is null)
        {
            throw new InvalidOperationException(
                "'Email' claim was not found.");
        }

        return email;
    }

    public static Email? TryGetEmail(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, EmailClaim, StringComparison.InvariantCultureIgnoreCase));

        if (claim is null)
            return null;

        return new Email(claim.Value);
    }

    public static Guid GetBoxLinkSessionIdOrThrow(this ClaimsPrincipal claimsPrincipal)
    {
        var claim = claimsPrincipal
            .Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, BoxLinkSessionIdClaim, StringComparison.InvariantCultureIgnoreCase));

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
                string.Equals(c.Type, BoxLinkSessionIdClaim, StringComparison.InvariantCultureIgnoreCase));
        
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
                string.Equals(c.Type, UserExternalIdClaim, StringComparison.InvariantCultureIgnoreCase));

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