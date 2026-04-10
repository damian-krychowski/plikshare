using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class WorkspaceRef
    {
        public required WorkspaceExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }
}
