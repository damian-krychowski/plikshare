using Microsoft.AspNetCore.DataProtection;
using PlikShare.Users.Id;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Cookie conventions for the user encryption session. One cookie per authenticated user
/// (name suffixed by the user's external id) — this prevents leaks when multiple users share
/// the same browser, since a leftover cookie belongs to its original user and cannot be
/// used by another authenticated identity.
///
/// The cookie value is the user's unwrapped X25519 private key, protected via
/// <see cref="IDataProtectionProvider"/> with <see cref="Purpose"/>.
/// </summary>
public static class UserEncryptionSessionCookie
{
    public const string NamePrefix = "UserEncryptionSession_";
    public const string Purpose = "UserEncryptionSession";

    public static string GetCookieName(UserExtId userExternalId) =>
        NamePrefix + userExternalId.Value;

    public static void Set(
        HttpContext httpContext,
        UserExtId userExternalId,
        ReadOnlySpan<byte> privateKey)
    {
        var protector = httpContext.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose);

        var protectedBytes = protector.Protect(privateKey.ToArray());
        var encoded = Convert.ToBase64String(protectedBytes);

        httpContext.Response.Cookies.Append(
            key: GetCookieName(userExternalId),
            value: encoded,
            options: new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true
            });
    }

    public static void Clear(
        HttpContext httpContext,
        UserExtId userExternalId)
    {
        httpContext.Response.Cookies.Delete(GetCookieName(userExternalId));
    }

    /// <summary>
    /// Reads and unprotects the user encryption cookie for the currently authenticated user.
    /// Returns null when the cookie is absent or fails to decrypt — callers that need to reject
    /// in those cases must do so themselves (e.g. the filter returns 423).
    /// </summary>
    public static byte[]? TryReadPrivateKey(HttpContext httpContext, UserExtId userExternalId)
    {
        var cookieName = GetCookieName(userExternalId);

        if (!httpContext.Request.Cookies.TryGetValue(cookieName, out var cookieValue)
            || string.IsNullOrEmpty(cookieValue))
        {
            return null;
        }

        var protector = httpContext.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose);

        try
        {
            var protectedBytes = Convert.FromBase64String(cookieValue);
            return protector.Unprotect(protectedBytes);
        }
        catch
        {
            return null;
        }
    }
}
