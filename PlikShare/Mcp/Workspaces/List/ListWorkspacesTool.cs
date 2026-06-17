using System.ComponentModel;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Mcp.Workspaces.List.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.List;

[McpServerToolType]
public class ListWorkspacesTool
{
    [McpServerTool(Name = AgentToolNames.ListWorkspaces)]
    [Description("Lists the workspaces this agent can access, with their external ids, names and current " +
                 "size in bytes. Use a returned workspaceExternalId as input to other tools such as create_folder.")]
    public static async Task<ListWorkspacesResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        GetAgentWorkspacesQuery getAgentWorkspacesQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

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
