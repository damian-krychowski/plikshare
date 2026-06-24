using Microsoft.AspNetCore.Http;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Mcp.BoxAccess.List.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.List;

/// <summary>
/// The reusable core of list_boxes: lists the boxes shared directly with the agent and writes the audit
/// entry. Called directly by the tool when no approval is required, and by the execute flow once a human
/// has approved the operation. The read is idempotent, so the execute flow simply re-lists.
/// </summary>
public class ListBoxesAgentOperation(
    GetAgentAccessibleBoxesQuery getAgentAccessibleBoxesQuery,
    AuditLogService auditLogService)
{
    public async Task<ListBoxesResponseDto> Execute(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var result = getAgentAccessibleBoxesQuery.Execute(agent);

        await auditLogService.Log(
            Audit.Agent.AccessibleBoxesListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                count: result.Boxes.Count),
            cancellationToken);

        return new ListBoxesResponseDto
        {
            Boxes = result.Boxes,
            Hint = result.Boxes.Count == 0
                ? "No boxes are shared directly with you. You may still be a member of workspaces, which " +
                  $"are separate from boxes - call {AgentToolNames.ListWorkspaces} to check."
                : null
        };
    }
}
