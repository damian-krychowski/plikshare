using Microsoft.AspNetCore.DataProtection;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.QuickShareExternalAccess.Authorization;

public class QuickShareUnlockSession(
    IDataProtectionProvider dataProtectionProvider,
    IClock clock)
{
    private const string Purpose = "QuickShareUnlockSession";
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan UnlockTtl = TimeSpan.FromMinutes(30);

    public static string CookieName(int quickShareId) => $"qs_session_{quickShareId}";

    public Payload ReadOrCreate(HttpContext httpContext, int quickShareId)
    {
        var cookieName = CookieName(quickShareId);

        if (httpContext.Request.Cookies.TryGetValue(cookieName, out var rawValue)
            && TryDecode(rawValue, out var existing)
            && existing.QuickShareId == quickShareId)
        {
            return existing;
        }

        var fresh = new Payload(
            SessionId: Guid.NewGuid(),
            QuickShareId: quickShareId,
            UnlockedUntil: null);

        WriteCookie(httpContext, cookieName, fresh);
        return fresh;
    }

    public void MarkUnlocked(HttpContext httpContext, Payload current)
    {
        var refreshed = current with
        {
            UnlockedUntil = clock.UtcNow.Add(UnlockTtl)
        };

        WriteCookie(httpContext, CookieName(current.QuickShareId), refreshed);
    }

    public bool IsUnlockValid(Payload session)
    {
        return session.UnlockedUntil is { } until && until > clock.UtcNow;
    }

    private void WriteCookie(HttpContext httpContext, string cookieName, Payload payload)
    {
        var protector = dataProtectionProvider.CreateProtector(Purpose);
        var protectedValue = protector.Protect(Json.Serialize(payload));

        httpContext.Response.Cookies.Append(
            cookieName,
            protectedValue,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                Path = "/api/quick-shares",
                Expires = clock.UtcNow.Add(SessionTtl)
            });
    }

    private bool TryDecode(string rawValue, out Payload payload)
    {
        try
        {
            var protector = dataProtectionProvider.CreateProtector(Purpose);
            var json = protector.Unprotect(rawValue);
            var decoded = Json.Deserialize<Payload>(json);

            if (decoded is null)
            {
                payload = default!;
                return false;
            }

            payload = decoded;
            return true;
        }
        catch
        {
            payload = default!;
            return false;
        }
    }

    public sealed record Payload(
        Guid SessionId,
        int QuickShareId,
        DateTimeOffset? UnlockedUntil);
}
