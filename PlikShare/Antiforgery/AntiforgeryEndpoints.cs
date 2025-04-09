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
}