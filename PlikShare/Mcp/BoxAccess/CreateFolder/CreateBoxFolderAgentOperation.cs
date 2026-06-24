using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Folders.Create;
using PlikShare.Folders.Id;
using PlikShare.Mcp.BoxAccess.CreateFolder.Contracts;
using PlikShare.Workspaces.Cache;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.CreateFolder;

/// <summary>
/// The reusable core of create_box_folder: re-validates the agent's box access and creates a folder inside
/// the box (defaulting to the box's root when no parent is given, and scoping the parent to the box's
/// subtree), writing the audit entry. Called directly by the tool when no approval is required, and by the
/// execute flow once a human has approved the operation.
/// </summary>
public class CreateBoxFolderAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    CreateFolderQuery createFolderQuery,
    AuditLogService auditLogService)
{
    public async Task<CreateBoxFolderResponseDto> Execute(
        HttpContext httpContext,
        CreateBoxFolderParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var boxAccess = await httpContext.GetAgentBoxAccess(
            agent,
            boxCache,
            boxAccessCache,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        if (boxAccess.IsOff)
            throw new McpException(
                $"Box '{parameters.BoxExternalId}' is disabled or exposes no folder, so its content cannot be changed.");

        var workspace = boxAccess.Box.Workspace;
        var boxFolder = boxAccess.Box.Folder!;

        var folderExternalId = FolderExtId.Parse(parameters.FolderExternalId);

        var parentFolderExternalId = string.IsNullOrWhiteSpace(parameters.ParentFolderExternalId)
            ? boxFolder.ExternalId
            : FolderExtId.Parse(parameters.ParentFolderExternalId);

        var result = await createFolderQuery.Execute(
            workspace: workspace,
            name: workspace.EncodeMetadata(
                value: parameters.Name,
                workspaceEncryptionSession: null),
            folderExternalId: folderExternalId,
            parentFolderExternalId: parentFolderExternalId,
            boxFolderId: boxFolder.Id,
            userIdentity: boxAccess.UserIdentity,
            cancellationToken: cancellationToken);

        switch (result)
        {
            case CreateFolderQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: folderExternalId,
                    buildEntry: folderRef => Audit.Folder.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        folder: folderRef,
                        box: boxAccess.ToAuditLogBoxRef()),
                    cancellationToken);

                return new CreateBoxFolderResponseDto
                {
                    FolderExternalId = folderExternalId.Value,
                    Name = parameters.Name,
                    ParentFolderExternalId = parentFolderExternalId.Value
                };

            case CreateFolderQuery.ResultCode.ParentFolderNotFound:
                throw new McpException(
                    $"Parent folder '{parentFolderExternalId}' was not found inside box '{parameters.BoxExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while creating folder: {result}.");
        }
    }
}
