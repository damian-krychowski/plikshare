using Microsoft.AspNetCore.Http;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Mcp.BoxAccess.GetDetails.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.GetDetails;

/// <summary>
/// The reusable core of get_box_details: re-validates the agent's direct access to the box and returns its
/// name, enabled state and the root folder it exposes, writing the audit entry. Called directly by the tool
/// when no approval is required, and by the execute flow once a human has approved the operation. The read
/// is idempotent, so the execute flow simply re-reads.
/// </summary>
public class GetBoxDetailsAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    AuditLogService auditLogService)
{
    public async Task<GetBoxDetailsResponseDto> Execute(
        HttpContext httpContext,
        GetBoxDetailsParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var boxAccess = await httpContext.GetAgentBoxAccess(
            agent,
            boxCache,
            boxAccessCache,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        var box = boxAccess.Box;

        await auditLogService.Log(
            Audit.Agent.BoxViewedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: box.Workspace.ExternalId.Value,
                boxExternalId: box.ExternalId.Value),
            cancellationToken);

        return new GetBoxDetailsResponseDto
        {
            ExternalId = box.ExternalId.Value,
            Name = box.Name,
            IsEnabled = !boxAccess.IsOff,
            RootFolderExternalId = box.Folder?.ExternalId.Value
        };
    }
}
