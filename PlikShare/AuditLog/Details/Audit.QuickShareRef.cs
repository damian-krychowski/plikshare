using PlikShare.QuickShares.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class QuickShareRef
    {
        public required QuickShareExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }
}
