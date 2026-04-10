using PlikShare.AuthProviders.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class AuthProviderRef
    {
        public required AuthProviderExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }
    }
}
