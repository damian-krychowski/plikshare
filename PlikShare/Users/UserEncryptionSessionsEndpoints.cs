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

    private static Ok<GetUserEncryptionSessionResponseDto> GetUnlockedSession(
        HttpContext httpContext)
    {
        var userExternalId = httpContext.User.GetExternalId();
        var cookieName = UserEncryptionSessionCookie.GetCookieName(userExternalId);

        var isUnlocked = httpContext.Request.Cookies.TryGetValue(cookieName, out var cookieValue)
                         && !string.IsNullOrEmpty(cookieValue);

        return TypedResults.Ok(new GetUserEncryptionSessionResponseDto(IsUnlocked: isUnlocked));
    }

    private static Ok LockSession(HttpContext httpContext)
    {
        var userExternalId = httpContext.User.GetExternalId();
        UserEncryptionSessionCookie.Clear(httpContext, userExternalId);

        return TypedResults.Ok();
    }
}

public record GetUserEncryptionSessionResponseDto(bool IsUnlocked);
