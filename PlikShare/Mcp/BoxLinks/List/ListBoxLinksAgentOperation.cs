using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Get;
using PlikShare.Boxes.Id;
using PlikShare.Mcp.BoxLinks.List.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxLinks.List;

/// <summary>
/// The reusable core of list_box_links: re-validates the agent's workspace access, resolves the box within
/// that workspace and lists its public links, writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation. The read is
/// idempotent, so the execute flow simply re-lists.
/// </summary>
public class ListBoxLinksAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxCache boxCache,
    GetBoxQuery getBoxQuery,
    AuditLogService auditLogService)
{
    public async Task<ListBoxLinksResponseDto> Execute(
        HttpContext httpContext,
        ListBoxLinksParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var box = await httpContext.GetAgentBoxInWorkspace(
            boxCache,
            workspace,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        var details = getBoxQuery.Execute(
            box: box,
            workspaceEncryptionSession: null);

        await auditLogService.Log(
            Audit.Agent.BoxLinksListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
                boxExternalId: parameters.BoxExternalId,
                count: details.Links.Count),
            cancellationToken);

        return new ListBoxLinksResponseDto
        {
            Links = details.Links
                .Select(link => new ListBoxLinksResponseDto.BoxLinkDto
                {
                    ExternalId = link.ExternalId,
                    Name = link.Name,
                    IsEnabled = link.IsEnabled,
                    AccessCode = link.AccessCode,
                    Permissions = new ListBoxLinksResponseDto.BoxLinkPermissionsDto
                    {
                        AllowDownload = link.Permissions.AllowDownload,
                        AllowUpload = link.Permissions.AllowUpload,
                        AllowList = link.Permissions.AllowList,
                        AllowDeleteFile = link.Permissions.AllowDeleteFile,
                        AllowRenameFile = link.Permissions.AllowRenameFile,
                        AllowMoveItems = link.Permissions.AllowMoveItems,
                        AllowCreateFolder = link.Permissions.AllowCreateFolder,
                        AllowRenameFolder = link.Permissions.AllowRenameFolder,
                        AllowDeleteFolder = link.Permissions.AllowDeleteFolder
                    },
                    WidgetOrigins = link.WidgetOrigins
                })
                .ToList()
        };
    }
}
