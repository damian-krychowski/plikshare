using Microsoft.AspNetCore.Http.HttpResults;
using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;

namespace PlikShare.Users;

/// <summary>
/// Endpoints for inspecting and locking the current user's encryption session.
/// The session itself is a DPAPI-protected cookie holding the unwrapped X25519 private key;
/// "locking" clears the cookie so the next encrypted operation will prompt for the encryption
/// password again.
/// </summary>
public static class UserEncryptionSessionsEndpoints
{
    public static void MapUserEncryptionSessionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/user-encryption-sessions")
            .WithTags("User Encryption Sessions")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapGet("/", GetUnlockedSession)
            .WithName("GetUserEncryptionSession");

        group.MapDelete("/", LockSession)
            .WithName("LockUserEncryptionSession");
    }

    private static GetUserEncryptionSessionResponseDto GetUnlockedSession(
        HttpContext httpContext)
    {
        var userExternalId = httpContext.User.GetExternalId();

        var hasCookie = httpContext
            .Request
            .Cookies
            .TryGetValue(UserEncryptionSessionCookie.CookieName(userExternalId), out var cookieValue);
        
        return new GetUserEncryptionSessionResponseDto(
            IsUnlocked: hasCookie && !string.IsNullOrEmpty(cookieValue));
    }

    private static Ok LockSession(HttpContext httpContext)
    {
        var userExternalId = httpContext.User.GetExternalId();

        httpContext.Response.Cookies.Delete(
            UserEncryptionSessionCookie.CookieName(userExternalId));

        return TypedResults.Ok();
    }
}

public record GetUserEncryptionSessionResponseDto(bool IsUnlocked);
