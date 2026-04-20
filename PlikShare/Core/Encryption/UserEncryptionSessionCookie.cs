using System.Security.Cryptography;
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
    public static string CookieName(UserExtId externalId) => $"UserEncryptionSession_{externalId.Value}";

    public const string Purpose = "UserEncryptionSession";
    
    public static void Set(
        HttpContext httpContext,
        UserExtId userExternalId,
        SecureBytes privateKey)
    {
        var protector = httpContext.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose);

        var encoded = privateKey.Use(protector, static (plaintext, p) =>
        {
            var plaintextCopy = plaintext.ToArray();

            try
            {
                var protectedBytes = p.Protect(plaintextCopy);
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintextCopy);
            }
        });

        httpContext.Response.Cookies.Append(
            key: CookieName(userExternalId),
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
    /// Cheap presence check — does the request carry our session cookie with a non-empty
    /// value? Does NOT unprotect the payload. Use this when the caller only needs to
    /// branch on "there is / isn't a session attempt" without paying the DataProtection
    /// cost (e.g. deciding whether to prompt for setup vs unlock).
    /// </summary>
    public static bool IsPresent(
        HttpContext httpContext,
        UserExtId userExternalId)
    {
        return httpContext.Request.Cookies.TryGetValue(CookieName(userExternalId), out var value)
            && !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Reads and unprotects the user encryption cookie for the given authenticated user.
    /// Returns a <see cref="SecureBytes"/> that the caller MUST dispose (use <c>using</c>).
    /// Returns null when the cookie is absent or fails to decrypt — callers that need to
    /// reject in those cases must do so themselves (e.g. the filter returns 423).
    /// </summary>
    public static SecureBytes? TryReadPrivateKey(
        HttpContext httpContext, 
        UserExtId userExternalId)
    {
        var cookieName = CookieName(userExternalId);

        if (!httpContext.Request.Cookies.TryGetValue(cookieName, out var cookieValue)
            || string.IsNullOrEmpty(cookieValue))
        {
            return null;
        }

        var protector = httpContext
            .RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose);

        byte[]? plaintextBytes = null;
        try
        {
            var protectedBytes = Convert.FromBase64String(cookieValue);
            plaintextBytes = protector.Unprotect(protectedBytes);

            if (plaintextBytes.Length != UserKeyPair.PrivateKeySize)
                return null;

            return SecureBytes.CopyFrom(plaintextBytes);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
        finally
        {
            if (plaintextBytes is not null)
                CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }
}
