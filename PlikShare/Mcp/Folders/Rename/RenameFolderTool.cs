using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.AuditLog;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Id;
using PlikShare.Folders.Rename;
using PlikShare.Mcp.Folders.Rename.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Folders.Rename;

[McpServerToolType]
public class RenameFolderTool
{
    [McpServerTool(Name = "rename_folder")]
    [Description("Renames an existing folder in a workspace the agent has access to.")]
    public static async Task<RenameFolderResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        UpdateFolderNameQuery updateFolderNameQuery,
        AuditLogService auditLogService,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the folder to rename.")]
        string folderExternalId,
        [Description("New name for the folder.")]
        string name,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var folderId = FolderExtId.Parse(folderExternalId);

        var resultCode = await updateFolderNameQuery.Execute(
            workspace: workspace,
            folderExternalId: folderId,
            name: workspace.EncodeMetadata(
                value: name,
                workspaceEncryptionSession: null),
            boxFolderId: null,
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
            isOperationAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFolderNameQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: folderId,
                    buildEntry: folderRef => Audit.Folder.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        folder: folderRef),
                    cancellationToken);

                return new RenameFolderResponseDto
                {
                    FolderExternalId = folderId.Value,
                    Name = name
                };

            case UpdateFolderNameQuery.ResultCode.FolderNotFound:
                throw new McpException(
                    $"Folder '{folderExternalId}' was not found in workspace '{workspaceExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while renaming folder: {resultCode}.");
        }
    }
}
