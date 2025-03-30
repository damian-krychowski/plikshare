using Microsoft.AspNetCore.Antiforgery;
using PlikShare.Core.Authorization;

namespace PlikShare.Antiforgery;

public static class AntiforgeryEndpoints
{
    public static void MapAntiforgeryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/antiforgery")
            .WithTags("AntiForgery");

        group.MapGet("/token", GetToken)
            .WithName("GetAntiforgeryToken")
            .AllowAnonymous();

        //that is required because otherwise - if I won't enforce boxLink cookie claims then
        //antiforgery token will be generated for anonymous access, because in contrast for internal policy
        //box link cookie is not auto-detected by the mechanism - I am not sure why
        group.MapGet("/box-link-token", GetBoxLinkToken)
            .WithName("GetAntiforgeryTokenForBoxLinks")
            .RequireAuthorization(policyNames: AuthPolicy.BoxLinkCookie);
    }
    
    public static void GetToken(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);

        context.Response.Cookies.Append(
            CookieName.Antiforgery, //this token name is automatically intercepted by angular on the frontend and converted into request header
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Strict,
                Secure = true
            });
    }
    
    public static void GetBoxLinkToken(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);

        context.Response.Cookies.Append(
            CookieName.BoxLinkAntiforgery,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Strict,
                Secure = true
            });
    }
}