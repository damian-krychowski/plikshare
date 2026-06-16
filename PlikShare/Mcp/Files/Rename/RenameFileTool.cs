using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.AuditLog;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Files.Rename;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.Rename;

[McpServerToolType]
public class RenameFileTool
{
    [McpServerTool(Name = "rename_file")]
    [Description("Renames an existing file in a workspace the agent can access. Only the file name changes; " +
                 "its extension is kept unchanged.")]
    public static async Task Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        UpdateFileNameQuery updateFileNameQuery,
        AuditLogService auditLogService,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the file to rename.")]
        string fileExternalId,
        [Description("New name for the file, without the extension (the extension is kept).")]
        string name,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var fileId = FileExtId.Parse(fileExternalId);

        var resultCode = await updateFileNameQuery.Execute(
            workspace: workspace,
            fileExternalId: fileId,
            name: workspace.EncodeMetadata(
                value: name,
                workspaceEncryptionSession: null),
            boxFolderId: null,
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
            isRenameAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFileNameQuery.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileId,
                    buildEntry: fileRef => Audit.File.RenamedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef),
                    cancellationToken);

                return;

            case UpdateFileNameQuery.ResultCode.FileNotFound:
                throw new McpException(
                    $"File '{fileExternalId}' was not found in workspace '{workspaceExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while renaming file: {resultCode}.");
        }
    }
}
