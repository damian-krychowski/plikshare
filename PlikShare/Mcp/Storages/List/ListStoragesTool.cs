using System.ComponentModel;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Mcp.Storages.List.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Storages.List;

[McpServerToolType]
public class ListStoragesTool
{
    [McpServerTool(Name = AgentToolNames.ListStorages)]
    [Description("Lists the storages this agent can use to create workspaces, with their external ids, names " +
                 "and encryption types. Pass a returned storageExternalId to create_workspace. Storages with " +
                 "full client-side encryption are omitted because agents cannot use them.")]
    public static async Task<ListStoragesResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        GetAgentStoragesQuery getAgentStoragesQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

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
