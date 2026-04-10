using PlikShare.BoxExternalAccess.Authorization;

namespace PlikShare.AuditLog;

static class BoxAccessAuditLogExtensions
{
    extension(BoxAccess boxAccess)
    {
        public AuditLogActorContext ToAuditLogActorContext(Guid correlationId) => new(
            Identity: boxAccess.UserIdentity,
            Email: boxAccess.UserEmail,
            Ip: boxAccess.UserIp,
            CorrelationId: correlationId);

        public AuditLogDetails.BoxRef ToAuditLogBoxRef() => new()
        {
            ExternalId = boxAccess.Box.ExternalId,
            Name = boxAccess.Box.Name,
            BoxLink = boxAccess.BoxLink is not null
                ? new AuditLogDetails.BoxLinkRef
                {
                    ExternalId = boxAccess.BoxLink.ExternalId,
                    Name = boxAccess.BoxLink.Name
                }
                : null
        };
    }
}