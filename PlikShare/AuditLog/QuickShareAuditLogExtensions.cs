using PlikShare.AuditLog.Details;
using PlikShare.QuickShareExternalAccess.Authorization;
using PlikShare.QuickShares.Cache;

namespace PlikShare.AuditLog;

static class QuickShareAuditLogExtensions
{
    extension(QuickShareContext quickShare)
    {
        public Audit.QuickShareRef ToAuditLogQuickShareRef() => new()
        {
            ExternalId = quickShare.ExternalId,
            Name = quickShare.Name
        };
    }

    extension(QuickShareAccess access)
    {
        public AuditLogActorContext ToAuditLogActorContext(Guid correlationId) => new(
            Identity: access.UserIdentity,
            Email: null,
            Ip: access.UserIp,
            CorrelationId: correlationId);
    }
}
