using Microsoft.AspNetCore.Http;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Mcp.Workspaces.List.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.List;

/// <summary>
/// The reusable core of list_workspaces: lists the workspaces the agent can access and writes the
/// audit entry. Called directly by the tool when no approval is required, and by the execute flow
/// once a human has approved the operation. The read is idempotent, so the execute flow re-reads.
/// </summary>
public class ListWorkspacesAgentOperation(
    GetAgentWorkspacesQuery getAgentWorkspacesQuery,
    AuditLogService auditLogService)
{
    public async Task<ListWorkspacesResponseDto> Execute(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var workspaces = getAgentWorkspacesQuery.Execute(agent);

        await auditLogService.Log(
            Audit.Agent.WorkspacesListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                count: workspaces.Count),
            cancellationToken);

        return new ListWorkspacesResponseDto
        {
            Workspaces = workspaces
        };
    }
}
