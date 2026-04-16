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
    public const string CookieName = "UserEncryptionSession";
    public const string Purpose = "UserEncryptionSession";
    
    public static void Set(
        HttpContext httpContext,
        ReadOnlySpan<byte> privateKey)
    {
        var protector = httpContext.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose);

        var protectedBytes = protector.Protect(privateKey.ToArray());
        var encoded = Convert.ToBase64String(protectedBytes);

        httpContext.Response.Cookies.Append(
            key: CookieName,
            value: encoded,
            options: new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true
            });
    }

    /// <summary>
    /// Reads and unprotects the user encryption cookie for the currently authenticated user.
    /// Returns null when the cookie is absent or fails to decrypt — callers that need to reject
    /// in those cases must do so themselves (e.g. the filter returns 423).
    /// </summary>
    public static byte[]? TryReadPrivateKey(HttpContext httpContext, UserExtId userExternalId)
    {
        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var cookieValue)
            || string.IsNullOrEmpty(cookieValue))
        {
            return null;
        }

        var protector = httpContext
            .RequestServices
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
