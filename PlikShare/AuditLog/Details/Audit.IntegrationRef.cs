using PlikShare.Integrations.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class IntegrationRef
    {
        public required IntegrationExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }
    }
}
