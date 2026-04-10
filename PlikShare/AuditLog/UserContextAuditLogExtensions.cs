using PlikShare.AuditLog.Details;
using PlikShare.Users.Cache;

namespace PlikShare.AuditLog;

static class UserContextAuditLogExtensions
{
    extension(UserContext user)
    {
        public Audit.UserRef ToAuditLogUserRef() => new()
        {
            ExternalId = user.ExternalId,
            Email = user.Email.Value
        };
    }
}
