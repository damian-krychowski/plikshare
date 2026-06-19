using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Cache;
using PlikShare.BoxLinks.Id;
using PlikShare.BoxLinks.UpdateIsEnabled;
using PlikShare.BoxLinks.UpdateName;
using PlikShare.BoxLinks.UpdatePermissions;
using PlikShare.BoxLinks.UpdateWidgetOrigins;
using PlikShare.Mcp.BoxLinks.Update.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxLinks.Update;

/// <summary>
/// The reusable core of update_box_link: re-validates the agent's workspace access, resolves the box link
/// within that workspace and applies the requested changes (name, enabled state, permissions and/or widget
/// origins), invalidating the box link cache and writing one audit entry per applied change. Permission
/// changes are merged over the link's current permissions, so a partial update only flips what is given.
/// Called directly by the tool when no approval is required, and by the execute flow once a human has
/// approved the operation.
/// </summary>
public class UpdateBoxLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxLinkCache boxLinkCache,
    UpdateBoxLinkNameQuery updateBoxLinkNameQuery,
    UpdateBoxLinkIsEnabledQuery updateBoxLinkIsEnabledQuery,
    UpdateBoxLinkPermissionsQuery updateBoxLinkPermissionsQuery,
    UpdateBoxLinkWidgetOriginsQuery updateBoxLinkWidgetOriginsQuery,
    AuditLogService auditLogService)
{
    public async Task<UpdateBoxLinkResponseDto> Execute(
        HttpContext httpContext,
        UpdateBoxLinkParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var boxLink = await httpContext.GetAgentBoxLinkInWorkspace(
            boxLinkCache,
            workspace,
            BoxLinkExtId.Parse(parameters.BoxLinkExternalId),
            cancellationToken);

        var trimmedName = parameters.Name?.Trim();

        if (parameters.Name is not null && string.IsNullOrWhiteSpace(trimmedName))
            throw new McpException("The box link name cannot be empty.");

        var hasPermissionChange = parameters.HasPermissionChange;

        if (trimmedName is null
            && parameters.IsEnabled is null
            && !hasPermissionChange
            && parameters.WidgetOrigins is null)
        {
            throw new McpException(
                "Provide at least one of name, isEnabled, a permission flag or widgetOrigins to update.");
        }

        var box = boxLink.Box;
        var boxRef = new Audit.BoxRef { ExternalId = box.ExternalId, Name = box.Name };

        if (trimmedName is not null)
        {
            var code = await updateBoxLinkNameQuery.Execute(boxLink, trimmedName, cancellationToken);

            if (code != UpdateBoxLinkNameQuery.ResultCode.Ok)
                throw new McpException($"Could not update the box link name: {code}.");

            await auditLogService.Log(
                Audit.BoxLink.NameUpdatedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    workspace: workspace.ToAuditLogWorkspaceRef(),
                    box: boxRef,
                    boxLink: new Audit.BoxLinkRef { ExternalId = boxLink.ExternalId, Name = trimmedName }),
                cancellationToken);
        }

        if (parameters.IsEnabled is not null)
        {
            var code = await updateBoxLinkIsEnabledQuery.Execute(boxLink, parameters.IsEnabled.Value, cancellationToken);

            if (code != UpdateBoxLinkIsEnabledQuery.ResultCode.Ok)
                throw new McpException($"Could not update the box link enabled state: {code}.");

            await auditLogService.Log(
                Audit.BoxLink.IsEnabledUpdatedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    workspace: workspace.ToAuditLogWorkspaceRef(),
                    box: boxRef,
                    boxLink: new Audit.BoxLinkRef { ExternalId = boxLink.ExternalId, Name = boxLink.Name },
                    isEnabled: parameters.IsEnabled.Value),
                cancellationToken);
        }

        if (hasPermissionChange)
        {
            var current = boxLink.Permissions;

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

            var code = await updateBoxLinkPermissionsQuery.Execute(boxLink, merged, cancellationToken);

            if (code != UpdateBoxLinkPermissionsQuery.ResultCode.Ok)
                throw new McpException($"Could not update the box link permissions: {code}.");

            await auditLogService.Log(
                Audit.BoxLink.PermissionsUpdatedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    workspace: workspace.ToAuditLogWorkspaceRef(),
                    box: boxRef,
                    boxLink: new Audit.BoxLinkRef { ExternalId = boxLink.ExternalId, Name = boxLink.Name },
                    permissions: merged),
                cancellationToken);
        }

        if (parameters.WidgetOrigins is not null)
        {
            var widgetOrigins = parameters.WidgetOrigins
                .Select(origin => (origin ?? string.Empty).Trim())
                .Where(origin => origin.Length > 0)
                .ToList();

            var code = await updateBoxLinkWidgetOriginsQuery.Execute(boxLink, widgetOrigins, cancellationToken);

            if (code != UpdateBoxLinkWidgetOriginsQuery.ResultCode.Ok)
                throw new McpException($"Could not update the box link widget origins: {code}.");

            await auditLogService.Log(
                Audit.BoxLink.WidgetOriginsUpdatedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    workspace: workspace.ToAuditLogWorkspaceRef(),
                    box: boxRef,
                    boxLink: new Audit.BoxLinkRef { ExternalId = boxLink.ExternalId, Name = boxLink.Name },
                    widgetOrigins: widgetOrigins),
                cancellationToken);
        }

        await boxLinkCache.InvalidateEntry(boxLink.Id, cancellationToken);

        return new UpdateBoxLinkResponseDto
        {
            BoxLinkExternalId = parameters.BoxLinkExternalId,
            UpdatedName = trimmedName is not null,
            UpdatedIsEnabled = parameters.IsEnabled is not null,
            UpdatedPermissions = hasPermissionChange,
            UpdatedWidgetOrigins = parameters.WidgetOrigins is not null
        };
    }
}
