using PlikShare.Core.Authorization;
using PlikShare.Core.Logging;
using PlikShare.Users.Entities;
using Serilog.Context;

namespace PlikShare.Users.Middleware;

public class UserLoggingMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task Invoke(HttpContext context)
    {
        if (context is { User.Identity.IsAuthenticated: true, User.Identity.AuthenticationType: AuthScheme.IdentityApplication })
        {
            using (LogContext.PushProperty(LogContextProperty.AuthPolicy, AuthPolicy.Internal))
            using (LogContext.PushProperty(LogContextProperty.UserExternalId, context.User.GetExternalId()))
            using (LogContext.PushProperty(LogContextProperty.UserEmail, EmailAnonymization.Anonymize(context.User.GetEmail())))
            {
                await _next(context);
            }
        }
        else if (context is { User.Identity.IsAuthenticated: true, User.Identity.AuthenticationType: AuthScheme.BoxLinkSessionScheme })
        {
            using (LogContext.PushProperty(LogContextProperty.AuthPolicy, AuthPolicy.BoxLinkCookie))
            using (LogContext.PushProperty(LogContextProperty.BoxLinkSessionId, context.User.GetBoxLinkSessionIdOrThrow()))
            {
                await _next(context);
            }
        }
        else
        {
            using (LogContext.PushProperty(LogContextProperty.AuthPolicy, "anonymous"))
            {
                await _next(context);
            }
        }
    }
}