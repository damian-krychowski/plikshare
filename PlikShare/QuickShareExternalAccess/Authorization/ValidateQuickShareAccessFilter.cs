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
        var accessCode = context.HttpContext.Request.RouteValues["accessCode"]?.ToString();

        if (string.IsNullOrWhiteSpace(accessCode))
            return HttpErrors.QuickShare.InvalidAccessCode();

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<QuickShareCache>();
        var clock = services.GetRequiredService<IClock>();
        var unlockSession = services.GetRequiredService<QuickShareUnlockSession>();

        var quickShare = await cache.TryGetQuickShareByAccessCode(
            accessCode: accessCode,
            cancellationToken: context.HttpContext.RequestAborted);

        if (quickShare is null || quickShare.Workspace.IsBeingDeleted)
            return HttpErrors.QuickShare.InvalidAccessCode();

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
