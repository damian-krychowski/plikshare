using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;
using PlikShare.Mcp.BulkDelete;
using PlikShare.Mcp.Files.BulkDownloadLink;
using PlikShare.Mcp.Files.Create;
using PlikShare.Mcp.Files.DownloadLink;
using PlikShare.Mcp.Files.Get;
using PlikShare.Mcp.Files.Read;
using PlikShare.Mcp.Files.Rename;
using PlikShare.Mcp.Folders.Create;
using PlikShare.Mcp.Folders.Rename;
using PlikShare.Mcp.MoveItems;
using PlikShare.Mcp.Search;
using PlikShare.Mcp.ShareLinks.Create;
using PlikShare.Mcp.ShareLinks.Delete;
using PlikShare.Mcp.ShareLinks.Get;
using PlikShare.Mcp.ShareLinks.List;
using PlikShare.Mcp.ShareLinks.Update;
using PlikShare.Mcp.Storages.List;
using PlikShare.Mcp.Workspaces.Content;
using PlikShare.Mcp.Workspaces.Create;
using PlikShare.Mcp.Workspaces.List;
using PlikShare.Mcp.Workspaces.Rename;

namespace PlikShare.Agents.Operations.Details;

/// <summary>
/// Routes an operation to the resolver that owns its tool. Each approval-capable tool has its own
/// resolver, listed explicitly in the constructor; supporting a new tool means adding its resolver
/// here and one branch below.
/// </summary>
public class AgentOperationDetailsResolver(
    BulkDeleteOperationDetailsResolver bulkDelete,
    DeleteShareLinkOperationDetailsResolver deleteShareLink,
    RenameFolderOperationDetailsResolver renameFolder,
    RenameFileOperationDetailsResolver renameFile,
    CreateFolderOperationDetailsResolver createFolder,
    MoveItemsOperationDetailsResolver moveItems,
    CreateFileOperationDetailsResolver createFile,
    RenameWorkspaceOperationDetailsResolver renameWorkspace,
    CreateShareLinkOperationDetailsResolver createShareLink,
    UpdateShareLinkOperationDetailsResolver updateShareLink,
    CreateWorkspaceOperationDetailsResolver createWorkspace,
    ReadFileOperationDetailsResolver readFile,
    GetFileOperationDetailsResolver getFile,
    GetFileDownloadLinkOperationDetailsResolver getFileDownloadLink,
    ListWorkspacesOperationDetailsResolver listWorkspaces,
    ListStoragesOperationDetailsResolver listStorages,
    ListShareLinksOperationDetailsResolver listShareLinks,
    GetShareLinkOperationDetailsResolver getShareLink,
    SearchOperationDetailsResolver search,
    ListWorkspaceContentOperationDetailsResolver listWorkspaceContent,
    GetBulkDownloadLinkOperationDetailsResolver getBulkDownloadLink)
{
    public AgentOperationDetails Resolve(AgentOperation operation)
    {
        if (operation.ToolName == AgentToolNames.BulkDelete)
            return bulkDelete.Resolve(operation);

        if (operation.ToolName == AgentToolNames.DeleteShareLink)
            return deleteShareLink.Resolve(operation);

        if (operation.ToolName == AgentToolNames.RenameFolder)
            return renameFolder.Resolve(operation);

        if (operation.ToolName == AgentToolNames.RenameFile)
            return renameFile.Resolve(operation);

        if (operation.ToolName == AgentToolNames.CreateFolder)
            return createFolder.Resolve(operation);

        if (operation.ToolName == AgentToolNames.MoveItems)
            return moveItems.Resolve(operation);

        if (operation.ToolName == AgentToolNames.CreateFile)
            return createFile.Resolve(operation);

        if (operation.ToolName == AgentToolNames.RenameWorkspace)
            return renameWorkspace.Resolve(operation);

        if (operation.ToolName == AgentToolNames.CreateShareLink)
            return createShareLink.Resolve(operation);

        if (operation.ToolName == AgentToolNames.UpdateShareLink)
            return updateShareLink.Resolve(operation);

        if (operation.ToolName == AgentToolNames.CreateWorkspace)
            return createWorkspace.Resolve(operation);

        if (operation.ToolName == AgentToolNames.ReadFile)
            return readFile.Resolve(operation);

        if (operation.ToolName == AgentToolNames.GetFile)
            return getFile.Resolve(operation);

        if (operation.ToolName == AgentToolNames.GetFileDownloadLink)
            return getFileDownloadLink.Resolve(operation);

        if (operation.ToolName == AgentToolNames.ListWorkspaces)
            return listWorkspaces.Resolve(operation);

        if (operation.ToolName == AgentToolNames.ListStorages)
            return listStorages.Resolve(operation);

        if (operation.ToolName == AgentToolNames.ListShareLinks)
            return listShareLinks.Resolve(operation);

        if (operation.ToolName == AgentToolNames.GetShareLink)
            return getShareLink.Resolve(operation);

        if (operation.ToolName == AgentToolNames.Search)
            return search.Resolve(operation);

        if (operation.ToolName == AgentToolNames.ListWorkspaceContent)
            return listWorkspaceContent.Resolve(operation);

        if (operation.ToolName == AgentToolNames.GetBulkDownloadLink)
            return getBulkDownloadLink.Resolve(operation);

        throw new InvalidOperationException(
            $"No details resolver for tool '{operation.ToolName}'.");
    }
}
