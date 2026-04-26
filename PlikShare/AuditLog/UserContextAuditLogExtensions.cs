using PlikShare.AuditLog.Details;
using PlikShare.Users.Cache;
using PlikShare.Users.GetOrCreate;

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

    extension(GetOrCreateUserInvitationQuery.User user)
    {
        public Audit.UserRef ToAuditLogUserRef() => new()
        {
            ExternalId = user.ExternalId,
            Email = user.Email.Value
        };
    }
}
