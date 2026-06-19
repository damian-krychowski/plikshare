using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.Boxes.List;
using PlikShare.Mcp.Boxes.List.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.List;

/// <summary>
/// The reusable core of list_boxes: re-validates the agent's workspace access and lists the workspace's
/// boxes, writing the audit entry. Called directly by the tool when no approval is required, and by the
/// execute flow once a human has approved the operation. The read is idempotent, so the execute flow
/// simply re-lists.
/// </summary>
public class ListBoxesAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    GetBoxesListQuery getBoxesListQuery,
    AuditLogService auditLogService)
{
    public async Task<ListBoxesResponseDto> Execute(
        HttpContext httpContext,
        ListBoxesParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var list = getBoxesListQuery.Execute(workspace);

        await auditLogService.Log(
            Audit.Agent.BoxesListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
                count: list.Items.Count),
            cancellationToken);

        return new ListBoxesResponseDto
        {
            Boxes = list.Items
                .Select(box => new ListBoxesResponseDto.BoxDto
                {
                    ExternalId = box.ExternalId.Value,
                    Name = box.Name,
                    IsEnabled = box.IsEnabled,
                    FolderPath = box.FolderPath
                        .Select(folder => new ListBoxesResponseDto.FolderPathItemDto
                        {
                            ExternalId = folder.ExternalId,
                            Name = folder.Name
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
