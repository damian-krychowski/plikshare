using PlikShare.EmailProviders.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class EmailProviderRef
    {
        public required EmailProviderExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }
    }
}
