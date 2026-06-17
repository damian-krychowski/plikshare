using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Create;
using PlikShare.Folders.Id;
using PlikShare.Mcp.Folders.Create.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Folders.Create;

[McpServerToolType]
public class CreateFolderTool
{
    [McpServerTool(Name = AgentToolNames.CreateFolder)]
    [Description("Creates a new folder in a workspace the agent has access to. " +
                 "Pass parentFolderExternalId to create a subfolder, or leave it empty to create a top-level folder.")]
    public static async Task<CreateFolderResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        CreateFolderQuery createFolderQuery,
        AuditLogService auditLogService,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("Name of the new folder.")]
        string name,
        [Description("Optional external id of the parent folder. Leave empty for a top-level folder.")]
        string? parentFolderExternalId = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var folderExternalId = FolderExtId.NewId();

        FolderExtId? parentExternalId = string.IsNullOrWhiteSpace(parentFolderExternalId)
            ? null
            : FolderExtId.Parse(parentFolderExternalId);

        var result = await createFolderQuery.Execute(
            workspace: workspace,
            folderExternalId: folderExternalId,
            parentFolderExternalId: parentExternalId,
            name: workspace.EncodeMetadata(
                value: name,
                workspaceEncryptionSession: null),
            boxFolderId: null,
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
            cancellationToken: cancellationToken);

        switch (result)
        {
            case CreateFolderQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: folderExternalId,
                    buildEntry: folderRef => Audit.Folder.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        folder: folderRef),
                    cancellationToken);

                return new CreateFolderResponseDto
                {
                    FolderExternalId = folderExternalId.Value,
                    Name = name,
                    ParentFolderExternalId = parentExternalId?.Value
                };

            case CreateFolderQuery.ResultCode.ParentFolderNotFound:
                throw new McpException(
                    $"Parent folder '{parentFolderExternalId}' was not found in workspace '{workspaceExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while creating folder: {result}.");
        }
    }
}
