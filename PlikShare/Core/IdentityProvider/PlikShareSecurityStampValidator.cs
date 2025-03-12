using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PlikShare.Core.Authorization;
using PlikShare.Users.Cache;

#pragma warning disable CS0618 // Type or member is obsolete

namespace PlikShare.Core.IdentityProvider;

public class PlikShareSecurityStampValidator : SecurityStampValidator<ApplicationUser>, ISecurityStampValidator
{
    private readonly UserCache _userCache;

    public PlikShareSecurityStampValidator(
        UserCache userCache,
        IOptions<SecurityStampValidatorOptions> options, 
        SignInManager<ApplicationUser> signInManager, 
        ISystemClock clock, 
        ILoggerFactory logger) : base(options, signInManager, clock, logger)
    {
        _userCache = userCache;
    }

    public PlikShareSecurityStampValidator(
        UserCache userCache,
        IOptions<SecurityStampValidatorOptions> options, 
        SignInManager<ApplicationUser> signInManager, 
        ILoggerFactory logger) : base(options, signInManager, logger)
    {
        _userCache = userCache;
    }

    public override async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var validate = await ShouldValidateAndRebuildPrincipal(
            context);

        if (validate)
        {
            var user = await VerifySecurityStamp(
                context.Principal);
            
            if (user != null)
            {
                await SecurityStampVerified(
                    user, 
                    context);
            }
            else
            {
                await SecurityStampRejected(
                    context);
            }
        }
        else
        {
            //if not enough time elapsed to run full original validation
            //i want to make sure that SecurityStamp has not changed in the meantime and check it via userCache
            if (context.Principal is null)
            {
                await SecurityStampRejected(context);
                return;
            }

            var userContext = await _userCache.TryGetUser(
                userExternalId: context.Principal.GetExternalId(),
                cancellationToken: context.HttpContext.RequestAborted);

            if (userContext is null || userContext.Stamps.Security != context.Principal.GetSecurityStamp())
            {
                await SecurityStampRejected(context);
            }
        }
    }

    private async ValueTask<bool> ShouldValidateAndRebuildPrincipal(CookieValidatePrincipalContext context)
    {
        if (HasEnoughTimeElapsed(context))
            return true;

        return await HasUserConcurrencyStampChanged(context);
    }

    private bool HasEnoughTimeElapsed(CookieValidatePrincipalContext context)
    {
        var currentUtc = TimeProvider.GetUtcNow();
        var issuedUtc = context.Properties.IssuedUtc;

        // Only validate if enough time has elapsed
        var validate = (issuedUtc == null);
        
        if (issuedUtc != null)
        {
            var timeElapsed = currentUtc.Subtract(issuedUtc.Value);
            validate = timeElapsed > Options.ValidationInterval;
        }

        return validate;
    }
    
    private async ValueTask<bool> HasUserConcurrencyStampChanged(
        CookieValidatePrincipalContext context)
    {
        if (context.Principal is null)
            return false;

        var userContext = await _userCache.TryGetUser(
            userExternalId: context.Principal.GetExternalId(),
            cancellationToken: context.HttpContext.RequestAborted);

        if (userContext is null)
            return false;

        return userContext.Stamps.Concurrency != context.Principal.GetConcurrencyStamp();
    }

    private async Task SecurityStampRejected(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await SignInManager.SignOutAsync();
        await SignInManager.Context.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);
    }

    protected override async Task SecurityStampVerified(
        ApplicationUser user, 
        CookieValidatePrincipalContext context)
    {
        var newPrincipal = await SignInManager.CreateUserPrincipalAsync(user);
        
        var newIdentity = newPrincipal.Identities.FirstOrDefault(
            identity => identity.AuthenticationType == IdentityConstants.ApplicationScheme);

        if (newIdentity is null)
            throw new InvalidOperationException(
                "ClaimsIdentity is null just after being recreated during SecurityStamp verification");
        
        var currentIdentity = context.Principal?.Identities.FirstOrDefault(
            identity => identity.AuthenticationType == IdentityConstants.ApplicationScheme);

        if (currentIdentity is not null)
        {
            newIdentity.CopyClaimIfExists(currentIdentity, Claims.RememberMe);
        }

        var userContext = await _userCache.TryGetUser(
            userId: user.DatabaseId,
            cancellationToken: context.HttpContext.RequestAborted);
        
        if(userContext is null)
            throw new InvalidOperationException(
                "UserContext is null just after UserPrincipal being recreated during SecurityStamp verification");
        
        newIdentity.AddClaims(
            claims: userContext.GetClaims());
        
        context.ReplacePrincipal(newPrincipal);
        context.ShouldRenew = true;

        if (!context.Options.SlidingExpiration)
        {
            // On renewal calculate the new ticket length relative to now to avoid
            // extending the expiration.
            context.Properties.IssuedUtc = TimeProvider.GetUtcNow();
        }
    }
    
}