using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.UpdateFolder;
using PlikShare.Boxes.UpdateIsEnabled;
using PlikShare.Boxes.UpdateName;
using PlikShare.Folders.Id;
using PlikShare.Mcp.Boxes.Update.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.Update;

/// <summary>
/// The reusable core of update_box: re-validates the agent's workspace access, resolves the box within
/// that workspace and applies the requested changes (name, enabled state and/or folder), invalidating the
/// box cache and writing one audit entry per applied change. Called directly by the tool when no approval
/// is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class UpdateBoxAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxCache boxCache,
    UpdateBoxNameQuery updateBoxNameQuery,
    UpdateBoxIsEnabledQuery updateBoxIsEnabledQuery,
    UpdateBoxFolderQuery updateBoxFolderQuery,
    AuditLogService auditLogService)
{
    public async Task<UpdateBoxResponseDto> Execute(
        HttpContext httpContext,
        UpdateBoxParams parameters,
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

        var trimmedName = parameters.Name?.Trim();

        if (parameters.Name is not null && string.IsNullOrWhiteSpace(trimmedName))
            throw new McpException("The box name cannot be empty.");

        if (trimmedName is null && parameters.IsEnabled is null && parameters.FolderExternalId is null)
            throw new McpException("Provide at least one of name, isEnabled or folderExternalId to update.");

        var effectiveName = trimmedName ?? box.Name;

        if (trimmedName is not null)
        {
            var code = await updateBoxNameQuery.Execute(box, trimmedName, cancellationToken);

            if (code != UpdateBoxNameQuery.ResultCode.Ok)
                throw new McpException($"Could not update the box name: {code}.");

            await auditLogService.Log(
                Audit.Box.NameUpdatedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    workspace: workspace.ToAuditLogWorkspaceRef(),
                    box: new Audit.BoxRef { ExternalId = box.ExternalId, Name = effectiveName }),
                cancellationToken);
        }

        if (parameters.IsEnabled is not null)
        {
            var code = await updateBoxIsEnabledQuery.Execute(box, parameters.IsEnabled.Value, cancellationToken);

            if (code != UpdateBoxIsEnabledQuery.ResultCode.Ok)
                throw new McpException($"Could not update the box enabled state: {code}.");

            await auditLogService.Log(
                Audit.Box.IsEnabledUpdatedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    workspace: workspace.ToAuditLogWorkspaceRef(),
                    box: new Audit.BoxRef { ExternalId = box.ExternalId, Name = effectiveName },
                    isEnabled: parameters.IsEnabled.Value),
                cancellationToken);
        }

        if (parameters.FolderExternalId is not null)
        {
            var folderExternalId = FolderExtId.Parse(parameters.FolderExternalId);

            var code = await updateBoxFolderQuery.Execute(box, folderExternalId, cancellationToken);

            switch (code)
            {
                case UpdateBoxFolderQuery.ResultCode.Ok:
                    await auditLogService.LogWithFolderContext(
                        folderExternalId: folderExternalId,
                        buildEntry: newFolderRef => Audit.Box.FolderUpdatedEntry(
                            actor: httpContext.GetAuditLogActorContext(),
                            workspace: workspace.ToAuditLogWorkspaceRef(),
                            box: new Audit.BoxRef { ExternalId = box.ExternalId, Name = effectiveName },
                            newFolder: newFolderRef),
                        cancellationToken);
                    break;

                case UpdateBoxFolderQuery.ResultCode.FolderNotFound:
                    throw new McpException(
                        $"Folder '{parameters.FolderExternalId}' was not found in the workspace.");

                default:
                    throw new McpException($"Could not update the box folder: {code}.");
            }
        }

        await boxCache.InvalidateEntry(box.Id, cancellationToken);

        return new UpdateBoxResponseDto
        {
            BoxExternalId = parameters.BoxExternalId,
            Name = trimmedName,
            IsEnabled = parameters.IsEnabled,
            FolderExternalId = parameters.FolderExternalId
        };
    }
}
