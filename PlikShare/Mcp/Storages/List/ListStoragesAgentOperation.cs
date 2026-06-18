using Microsoft.AspNetCore.Http;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Mcp.Storages.List.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Storages.List;

/// <summary>
/// The reusable core of list_storages: lists the storages the agent can use to create workspaces and
/// writes the audit entry. Called directly by the tool when no approval is required, and by the
/// execute flow once a human has approved the operation. The read is idempotent, so it re-reads.
/// </summary>
public class ListStoragesAgentOperation(
    GetAgentStoragesQuery getAgentStoragesQuery,
    AuditLogService auditLogService)
{
    public async Task<ListStoragesResponseDto> Execute(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var storages = getAgentStoragesQuery.Execute(agent);

        await auditLogService.Log(
            Audit.Agent.StoragesListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                count: storages.Count),
            cancellationToken);

        return new ListStoragesResponseDto
        {
            Storages = storages
        };
    }
}
