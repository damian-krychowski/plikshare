using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Folders.Id;
using PlikShare.Folders.List;
using PlikShare.Mcp.BoxAccess.Content.Contracts;

namespace PlikShare.Mcp.BoxAccess.Content;

/// <summary>
/// The reusable core of list_box_content: re-validates the agent's box access and lists the folders and
/// files directly inside the box's root folder (or one of its subfolders), scoped to the box's subtree.
/// Called directly by the tool when no approval is required, and by the execute flow once a human has
/// approved the operation. The read is idempotent, so the execute flow simply re-lists.
/// </summary>
public class ListBoxContentAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    GetFolderContentQuery getFolderContentQuery)
{
    public async Task<ListBoxContentResponseDto> Execute(
        HttpContext httpContext,
        ListBoxContentParams parameters,
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
                $"Box '{parameters.BoxExternalId}' is disabled or exposes no folder, so its content cannot be listed.");

        var boxFolder = boxAccess.Box.Folder!;

        var folderExternalId = string.IsNullOrWhiteSpace(parameters.FolderExternalId)
            ? boxFolder.ExternalId
            : FolderExtId.Parse(parameters.FolderExternalId);

        var content = getFolderContentQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            folderExternalId: folderExternalId,
            boxFolderId: boxFolder.Id,
            userIdentity: boxAccess.UserIdentity,
            executionFlags: new GetFolderContentQuery.ExecutionFlags(
                GetCurrentFolder: true,
                GetSubfolders: true,
                GetFiles: GetFolderContentQuery.FilesExecutionFlag.All,
                GetUploads: false,
                ExposeCreatedAt: false),
            workspaceEncryptionSession: null);

        if (content is null)
            throw new McpException(
                $"Folder '{folderExternalId}' was not found inside box '{parameters.BoxExternalId}'.");

        return new ListBoxContentResponseDto
        {
            FolderExternalId = content.Folder?.ExternalId ?? folderExternalId.Value,
            Folders = (content.Subfolders ?? [])
                .Select(folder => new ListBoxContentResponseDto.FolderDto
                {
                    ExternalId = folder.ExternalId,
                    Name = folder.Name
                })
                .ToList(),
            Files = (content.Files ?? [])
                .Select(file => new ListBoxContentResponseDto.FileDto
                {
                    ExternalId = file.ExternalId,
                    Name = file.Name,
                    Extension = file.Extension,
                    SizeInBytes = file.SizeInBytes
                })
                .ToList()
        };
    }
}
