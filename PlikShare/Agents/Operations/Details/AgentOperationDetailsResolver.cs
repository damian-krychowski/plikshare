using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;
using PlikShare.Mcp.Boxes.Create;
using PlikShare.Mcp.Boxes.Delete;
using PlikShare.Mcp.Boxes.Get;
using PlikShare.Mcp.Boxes.List;
using PlikShare.Mcp.Boxes.Members.Invite;
using PlikShare.Mcp.Boxes.Members.List;
using PlikShare.Mcp.Boxes.Members.Revoke;
using PlikShare.Mcp.Boxes.Members.UpdatePermissions;
using PlikShare.Mcp.Boxes.Update;
using PlikShare.Mcp.BoxLinks.Create;
using PlikShare.Mcp.BoxLinks.Delete;
using PlikShare.Mcp.BoxLinks.List;
using PlikShare.Mcp.BoxLinks.RegenerateAccessCode;
using PlikShare.Mcp.BoxLinks.Update;
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
using PlikShare.Mcp.Workspaces.Members.Invite;
using PlikShare.Mcp.Workspaces.Members.List;
using PlikShare.Mcp.Workspaces.Members.Revoke;
using PlikShare.Mcp.Workspaces.Members.UpdatePermissions;
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
    GetBulkDownloadLinkOperationDetailsResolver getBulkDownloadLink,
    ListWorkspaceMembersOperationDetailsResolver listWorkspaceMembers,
    InviteWorkspaceMembersOperationDetailsResolver inviteWorkspaceMembers,
    UpdateWorkspaceMemberPermissionsOperationDetailsResolver updateWorkspaceMemberPermissions,
    RevokeWorkspaceMemberOperationDetailsResolver revokeWorkspaceMember,
    ListBoxesOperationDetailsResolver listBoxes,
    GetBoxOperationDetailsResolver getBox,
    CreateBoxOperationDetailsResolver createBox,
    UpdateBoxOperationDetailsResolver updateBox,
    DeleteBoxOperationDetailsResolver deleteBox,
    ListBoxLinksOperationDetailsResolver listBoxLinks,
    CreateBoxLinkOperationDetailsResolver createBoxLink,
    UpdateBoxLinkOperationDetailsResolver updateBoxLink,
    DeleteBoxLinkOperationDetailsResolver deleteBoxLink,
    RegenerateBoxLinkAccessCodeOperationDetailsResolver regenerateBoxLinkAccessCode,
    ListBoxMembersOperationDetailsResolver listBoxMembers,
    InviteBoxMembersOperationDetailsResolver inviteBoxMembers,
    UpdateBoxMemberPermissionsOperationDetailsResolver updateBoxMemberPermissions,
    RevokeBoxMemberOperationDetailsResolver revokeBoxMember)
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

        if (operation.ToolName == AgentToolNames.ListWorkspaceMembers)
            return listWorkspaceMembers.Resolve(operation);

        if (operation.ToolName == AgentToolNames.InviteWorkspaceMembers)
            return inviteWorkspaceMembers.Resolve(operation);

        if (operation.ToolName == AgentToolNames.UpdateWorkspaceMemberPermissions)
            return updateWorkspaceMemberPermissions.Resolve(operation);

        if (operation.ToolName == AgentToolNames.RevokeWorkspaceMember)
            return revokeWorkspaceMember.Resolve(operation);

        if (operation.ToolName == AgentToolNames.ListBoxes)
            return listBoxes.Resolve(operation);

        if (operation.ToolName == AgentToolNames.GetBox)
            return getBox.Resolve(operation);

        if (operation.ToolName == AgentToolNames.CreateBox)
            return createBox.Resolve(operation);

        if (operation.ToolName == AgentToolNames.UpdateBox)
            return updateBox.Resolve(operation);

        if (operation.ToolName == AgentToolNames.DeleteBox)
            return deleteBox.Resolve(operation);

        if (operation.ToolName == AgentToolNames.ListBoxLinks)
            return listBoxLinks.Resolve(operation);

        if (operation.ToolName == AgentToolNames.CreateBoxLink)
            return createBoxLink.Resolve(operation);

        if (operation.ToolName == AgentToolNames.UpdateBoxLink)
            return updateBoxLink.Resolve(operation);

        if (operation.ToolName == AgentToolNames.DeleteBoxLink)
            return deleteBoxLink.Resolve(operation);

        if (operation.ToolName == AgentToolNames.RegenerateBoxLinkAccessCode)
            return regenerateBoxLinkAccessCode.Resolve(operation);

        if (operation.ToolName == AgentToolNames.ListBoxMembers)
            return listBoxMembers.Resolve(operation);

        if (operation.ToolName == AgentToolNames.InviteBoxMembers)
            return inviteBoxMembers.Resolve(operation);

        if (operation.ToolName == AgentToolNames.UpdateBoxMemberPermissions)
            return updateBoxMemberPermissions.Resolve(operation);

        if (operation.ToolName == AgentToolNames.RevokeBoxMember)
            return revokeBoxMember.Resolve(operation);

        throw new InvalidOperationException(
            $"No details resolver for tool '{operation.ToolName}'.");
    }
}
