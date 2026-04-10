using PlikShare.Users.Cache;

namespace PlikShare.AuditLog;

static class UserContextAuditLogExtensions
{
    extension(UserContext user)
    {
        public AuditLogDetails.UserRef ToAuditLogUserRef() => new()
        {
            ExternalId = user.ExternalId,
            Email = user.Email.Value
        };
    }
}
