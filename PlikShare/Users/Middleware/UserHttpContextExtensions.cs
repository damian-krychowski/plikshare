using PlikShare.Core.Authorization;
using PlikShare.Users.Cache;

namespace PlikShare.Users.Middleware;

public static class UserHttpContextExtensions
{
    private const string UserContextKey = "user-context";

    public static async ValueTask<UserContext> GetUserContext(
        this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(UserContextKey, out var cached)
            && cached is UserContext userContext)
        {
            return userContext;
        }

        var externalId = httpContext.User.GetExternalId();

        var userCache = httpContext
            .RequestServices
            .GetRequiredService<UserCache>();

        var context = await userCache.GetOrThrow(
            externalId,
            httpContext.RequestAborted);

        httpContext.Items[UserContextKey] = context;

        return context;
    }
}