using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.UserIdentity;

namespace PlikShare.AuditLog;

public static class AuditLogHttpContextExtensions
{
    public static AuditLogActorContext GetAuditLogActorContext(this HttpContext httpContext)
    {
        var userIdentity = TryGetUserIdentity(httpContext);
        var email = httpContext.User.TryGetEmail();
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = httpContext.GetCorrelationId();

        return new AuditLogActorContext(
            Identity: userIdentity ?? AnonymousIdentity.Instance,
            Email: email?.Value,
            Ip: ip,
            CorrelationId: correlationId);
    }

    private static IUserIdentity? TryGetUserIdentity(HttpContext httpContext)
    {
        try
        {
            var externalId = httpContext.User.GetExternalId();
            return new UserIdentity(externalId);
        }
        catch
        {
            return null;
        }
    }
}

public record AuditLogActorContext(
    IUserIdentity Identity,
    string? Email,
    string? Ip,
    Guid CorrelationId);
