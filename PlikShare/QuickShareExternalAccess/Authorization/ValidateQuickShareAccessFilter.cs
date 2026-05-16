using System.Security.Cryptography;
using PlikShare.Core.Clock;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.QuickShares.Cache;

namespace PlikShare.QuickShareExternalAccess.Authorization;

public class ValidateQuickShareAccessFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var slug = context.HttpContext.Request.RouteValues["slug"]?.ToString();

        if (string.IsNullOrWhiteSpace(slug))
            return HttpErrors.QuickShare.InvalidSlug();

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<QuickShareCache>();
        var clock = services.GetRequiredService<IClock>();
        var unlockSession = services.GetRequiredService<QuickShareUnlockSession>();

        var quickShare = await cache.TryGetQuickShareBySlug(
            slug: slug,
            cancellationToken: context.HttpContext.RequestAborted);

        if (quickShare is null || quickShare.Workspace.IsBeingDeleted)
            return HttpErrors.QuickShare.InvalidSlug();

        if (quickShare.SecretHash is not null)
        {
            var token = context.HttpContext.Request.Query["token"].ToString();

            if (string.IsNullOrEmpty(token))
                return HttpErrors.QuickShare.SecretRequired();

            var providedHash = QuickShareCache.HashSecret(token);
            if (!CryptographicOperations.FixedTimeEquals(providedHash, quickShare.SecretHash))
                return HttpErrors.QuickShare.InvalidSecret();
        }

        if (quickShare.ExpiresAt is { } expiresAt && expiresAt <= clock.UtcNow)
            return HttpErrors.QuickShare.Expired();

        if (quickShare.MaxDownloads is { } max && quickShare.DownloadsCount >= max)
            return HttpErrors.QuickShare.Exhausted();

        var session = unlockSession.ReadOrCreate(
            httpContext: context.HttpContext,
            quickShareId: quickShare.Id);

        if (quickShare.PasswordHash is not null && !unlockSession.IsUnlockValid(session))
            return HttpErrors.QuickShare.RequiresPassword();

        var access = new QuickShareAccess(
            QuickShare: quickShare,
            UserIdentity: new QuickShareSessionUserIdentity(session.SessionId),
            UserIp: context.HttpContext.Connection.RemoteIpAddress?.ToString());

        context.HttpContext.Items[QuickShareAccess.HttpContextName] = access;

        return await next(context);
    }
}
