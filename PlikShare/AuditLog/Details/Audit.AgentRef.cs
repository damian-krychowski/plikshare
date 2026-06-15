using PlikShare.Agents.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class AgentRef
    {
        public required AgentExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }
}
