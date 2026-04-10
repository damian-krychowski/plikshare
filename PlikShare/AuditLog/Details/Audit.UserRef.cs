using PlikShare.Users.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class UserRef
    {
        public required UserExtId ExternalId { get; init; }
        public required string Email { get; init; }
    }
}
