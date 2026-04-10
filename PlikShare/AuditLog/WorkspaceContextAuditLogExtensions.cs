using PlikShare.AuditLog.Details;
using PlikShare.Workspaces.Cache;

namespace PlikShare.AuditLog;

static class WorkspaceContextAuditLogExtensions
{
    extension(WorkspaceContext workspace)
    {
        public Audit.WorkspaceRef ToAuditLogWorkspaceRef() => new()
        {
            ExternalId = workspace.ExternalId,
            Name = workspace.Name
        };
    }
}
