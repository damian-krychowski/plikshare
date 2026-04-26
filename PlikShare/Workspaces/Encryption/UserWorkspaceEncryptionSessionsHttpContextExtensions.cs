using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Users.Middleware;

namespace PlikShare.Workspaces.Encryption;

public static class UserWorkspaceEncryptionSessionsHttpContextExtensions
{
    public static async ValueTask<AllUserWorkspaceEncryptionSessions> GetUserWorkspaceEncryptionSessions(
        this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(AllUserWorkspaceEncryptionSessions.HttpContextName, out var cached)
            && cached is AllUserWorkspaceEncryptionSessions sessions)
        {
            return sessions;
        }

        var built = await BuildSessions(httpContext);

        httpContext.Items[AllUserWorkspaceEncryptionSessions.HttpContextName] = built;
        httpContext.Response.RegisterForDispose(built);

        return built;
    }

    private static async ValueTask<AllUserWorkspaceEncryptionSessions> BuildSessions(
        HttpContext httpContext)
    {
        var userExternalId = httpContext.User.GetExternalId();

        if (!UserEncryptionSessionCookie.IsPresent(httpContext, userExternalId))
            return AllUserWorkspaceEncryptionSessions.Empty;

        using var privateKey = UserEncryptionSessionCookie.TryReadPrivateKey(
            httpContext, userExternalId);

        if (privateKey is null)
            return AllUserWorkspaceEncryptionSessions.Empty;

        var user = await httpContext.GetUserContext();

        var loader = httpContext
            .RequestServices
            .GetRequiredService<UserWorkspaceEncryptionSessionsLoader>();

        return loader.LoadForUser(user, privateKey);
    }
}
