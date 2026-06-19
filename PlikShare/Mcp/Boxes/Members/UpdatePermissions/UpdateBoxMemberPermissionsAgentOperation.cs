using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Members.UpdatePermissions;
using PlikShare.Boxes.Permissions;
using PlikShare.Mcp.Boxes.Members.UpdatePermissions.Contracts;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.Members.UpdatePermissions;

/// <summary>
/// The reusable core of update_box_member_permissions: re-validates the agent's workspace access, resolves
/// the box membership within that workspace and updates the member's permissions, invalidating the
/// membership cache and writing the audit entry. Permission changes are merged over the member's current
/// permissions, so a partial update only flips what is given. Called directly by the tool when no approval
/// is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class UpdateBoxMemberPermissionsAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxMembershipCache boxMembershipCache,
    UpdateBoxMemberPermissionsQuery updateBoxMemberPermissionsQuery,
    AuditLogService auditLogService)
{
    public async Task<UpdateBoxMemberPermissionsResponseDto> Execute(
        HttpContext httpContext,
        UpdateBoxMemberPermissionsParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        if (!parameters.HasPermissionChange)
            throw new McpException("Provide at least one permission flag to update.");

        var boxMembership = await httpContext.GetAgentBoxMembershipInWorkspace(
            boxMembershipCache,
            workspace,
            BoxExtId.Parse(parameters.BoxExternalId),
            UserExtId.Parse(parameters.MemberExternalId),
            cancellationToken);

        var current = boxMembership.Permissions;

        var merged = new BoxPermissions(
            AllowDownload: parameters.AllowDownload ?? current.AllowDownload,
            AllowUpload: parameters.AllowUpload ?? current.AllowUpload,
            AllowList: parameters.AllowList ?? current.AllowList,
            AllowDeleteFile: parameters.AllowDeleteFile ?? current.AllowDeleteFile,
            AllowRenameFile: parameters.AllowRenameFile ?? current.AllowRenameFile,
            AllowMoveItems: parameters.AllowMoveItems ?? current.AllowMoveItems,
            AllowCreateFolder: parameters.AllowCreateFolder ?? current.AllowCreateFolder,
            AllowRenameFolder: parameters.AllowRenameFolder ?? current.AllowRenameFolder,
            AllowDeleteFolder: parameters.AllowDeleteFolder ?? current.AllowDeleteFolder);

        await updateBoxMemberPermissionsQuery.Execute(
            boxMembership: boxMembership,
            permissions: merged,
            cancellationToken: cancellationToken);

        await boxMembershipCache.InvalidateEntry(boxMembership, cancellationToken);

        await auditLogService.LogWithFolderContext(
            folderExternalId: boxMembership.Box.Folder?.ExternalId,
            buildEntry: folderRef => Audit.Box.MemberPermissionsUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspace.ToAuditLogWorkspaceRef(),
                box: new Audit.BoxRef
                {
                    ExternalId = boxMembership.Box.ExternalId,
                    Name = boxMembership.Box.Name,
                    Folder = folderRef
                },
                member: boxMembership.Member.ToAuditLogUserRef(),
                permissions: merged),
            cancellationToken);

        return new UpdateBoxMemberPermissionsResponseDto
        {
            MemberExternalId = parameters.MemberExternalId,
            Permissions = new UpdateBoxMemberPermissionsResponseDto.PermissionsDto
            {
                AllowDownload = merged.AllowDownload,
                AllowUpload = merged.AllowUpload,
                AllowList = merged.AllowList,
                AllowDeleteFile = merged.AllowDeleteFile,
                AllowRenameFile = merged.AllowRenameFile,
                AllowMoveItems = merged.AllowMoveItems,
                AllowCreateFolder = merged.AllowCreateFolder,
                AllowRenameFolder = merged.AllowRenameFolder,
                AllowDeleteFolder = merged.AllowDeleteFolder
            }
        };
    }
}
