using Microsoft.AspNetCore.Identity;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using Serilog;

namespace PlikShare.Core.Authorization;

public static class AuthPolicy
{
    public const string BoxLinkCookie = "box-link-cookie";
    public const string Internal = "internal";
    public const string InternalOrBoxLink = "internal-or-box-link";
}

public static class AuthScheme
{
    public const string BoxLinkSessionScheme = "box-link-cookie-schema";
    
    //the same value as  IdentityConstants.ApplicationScheme
    public const string IdentityApplication = "Identity.Application";
}

public static class CookieName
{
    public const string SessionAuth = "SessionAuth";
    public const string BoxLinkAuth ="BoxLinkAuth";
    public const string TwoFactorUserId = "Identity.TwoFactorUserId";
    public const string TwoFactorRememberMe = "Identity.TwoFactorRememberMe";
}

public static class AuthorizationStartupExtensions
{
    public static void SetupAuth(this WebApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Services.AddSingleton(
            GetAppOwnersOrThrow(app));

        app.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
        });
        
        app.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicy.Internal, policy =>
            {
                policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme);
                policy.RequireAuthenticatedUser();
            });
            
            options.AddPolicy(AuthPolicy.BoxLinkCookie, policy =>
            {
                policy.AddAuthenticationSchemes(AuthScheme.BoxLinkSessionScheme);
                policy.RequireClaim(Claims.BoxLinkSessionId);
            });

            //this policy is a mix of two polices above
            //it is designed to be able to check if the pre-signed links are accessed by people who generated them
            //that is and additional layer for in-app pre-singed links
            //however the s3 pre-signed links do not follow this auth, because they are hitting directly the s3 servers
            options.AddPolicy(AuthPolicy.InternalOrBoxLink, policy =>
            {
                policy.AddAuthenticationSchemes(
                    IdentityConstants.ApplicationScheme, 
                    AuthScheme.BoxLinkSessionScheme);

                policy.RequireAssertion(context =>
                {
                    // Check for Internal policy conditions
                    var isInternalAuth = context.User.Identity?.AuthenticationType == IdentityConstants.ApplicationScheme
                                         && context.User.Identity.IsAuthenticated;

                    // Check for BoxLink policy conditions
                    var isBoxLinkAuth = context.User.Identity?.AuthenticationType == AuthScheme.BoxLinkSessionScheme
                                        && context.User.HasClaim(c => c.Type == Claims.BoxLinkSessionId);

                    return isInternalAuth || isBoxLinkAuth;
                });
            });
        });
            
        var authenticationBuilder = app.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });
        
        authenticationBuilder.AddIdentityCookies(cookies =>
        {
            cookies.ApplicationCookie!.Configure(options =>
            {
                options.Cookie.Name = CookieName.SessionAuth;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = 403;
                    return Task.CompletedTask;
                };

                options.Events.OnSigningIn =  async context =>
                {
                    var identity = context.Principal?.Identities.FirstOrDefault(
                        identity => identity.AuthenticationType == IdentityConstants.ApplicationScheme);

                    if (identity is null)
                        return;

                    var userCache = context
                        .HttpContext
                        .RequestServices
                        .GetRequiredService<UserCache>();

                    var user = await userCache.TryGetUser(
                        userExternalId: identity.GetExternalId(),
                        cancellationToken: context.HttpContext.RequestAborted);

                    if (user is null)
                        throw new InvalidOperationException(
                            $"SignedIn user '{identity.GetExternalId()}' was not found in cache ");

                    identity.AddClaims(
                        claims: user.GetClaims());
                };
            });
        });
        
        authenticationBuilder.AddCookie(AuthScheme.BoxLinkSessionScheme, options =>
        {
            options.Cookie.Name = CookieName.BoxLinkAuth;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        });
        
        Log.Information("[SETUP] Auth setup finished.");
    }
    
    private static AppOwners GetAppOwnersOrThrow(WebApplicationBuilder app)
    {
        var ownersStr = app.Configuration.GetValue<string>(
            "AppOwners");
        
        if (string.IsNullOrWhiteSpace(ownersStr))
        {
            throw new InvalidOperationException(
                "Cannot start application if AppOwners are not set.");
        }
        
        var ownersInitialPassword = app.Configuration.GetValue<string>(
            "AppOwnersInitialPassword");

        if (string.IsNullOrWhiteSpace(ownersInitialPassword))
        {
            throw new InvalidOperationException(
                "Cannot start application if AppOwnersInitialPassword is not set.");
        }

        var owners = ownersStr
            .Split(",", StringSplitOptions.RemoveEmptyEntries)
            .Select(owner => new Email(owner))
            .ToList();

        return new AppOwners(
            owners, 
            ownersInitialPassword);
    }
}