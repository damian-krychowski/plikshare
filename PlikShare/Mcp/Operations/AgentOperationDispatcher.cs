using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxAccess.BulkDownloadLink;
using PlikShare.Mcp.BoxAccess.Content;
using PlikShare.Mcp.BoxAccess.CreateFile;
using PlikShare.Mcp.BoxAccess.CreateFolder;
using PlikShare.Mcp.BoxAccess.Delete;
using PlikShare.Mcp.BoxAccess.DownloadLink;
using PlikShare.Mcp.BoxAccess.GetDetails;
using PlikShare.Mcp.BoxAccess.List;
using PlikShare.Mcp.BoxAccess.MoveItems;
using PlikShare.Mcp.BoxAccess.ReadFile;
using PlikShare.Mcp.BoxAccess.RenameFile;
using PlikShare.Mcp.BoxAccess.RenameFolder;
using PlikShare.Mcp.BoxAccess.Search;
using PlikShare.Mcp.Boxes.Create;
using PlikShare.Mcp.Boxes.Delete;
using PlikShare.Mcp.Boxes.Get;
using PlikShare.Mcp.Boxes.ListWorkspaceBoxes;
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

namespace PlikShare.Mcp.Operations;

/// <summary>
/// Resolves an approved operation into a plan: how to run it and whether its result must be
/// persisted. Each approval-capable tool owns one branch and declares its own
/// <see cref="AgentOperationPlan.PersistsResult"/> — mutating tools persist (exactly-once,
/// the commit never re-runs them), idempotent read tools do not (the commit simply re-reads).
/// </summary>
public class AgentOperationDispatcher(
    BulkDeleteAgentOperation bulkDeleteOperation,
    DeleteShareLinkAgentOperation deleteShareLinkOperation,
    RenameFolderAgentOperation renameFolderOperation,
    RenameFileAgentOperation renameFileOperation,
    CreateFolderAgentOperation createFolderOperation,
    MoveItemsAgentOperation moveItemsOperation,
    CreateFileAgentOperation createFileOperation,
    RenameWorkspaceAgentOperation renameWorkspaceOperation,
    CreateShareLinkAgentOperation createShareLinkOperation,
    UpdateShareLinkAgentOperation updateShareLinkOperation,
    CreateWorkspaceAgentOperation createWorkspaceOperation,
    ReadFileAgentOperation readFileOperation,
    GetFileAgentOperation getFileOperation,
    GetFileDownloadLinkAgentOperation getFileDownloadLinkOperation,
    ListWorkspacesAgentOperation listWorkspacesOperation,
    ListStoragesAgentOperation listStoragesOperation,
    ListShareLinksAgentOperation listShareLinksOperation,
    GetShareLinkAgentOperation getShareLinkOperation,
    SearchAgentOperation searchOperation,
    ListWorkspaceContentAgentOperation listWorkspaceContentOperation,
    GetBulkDownloadLinkAgentOperation getBulkDownloadLinkOperation,
    ListWorkspaceMembersAgentOperation listWorkspaceMembersOperation,
    InviteWorkspaceMembersAgentOperation inviteWorkspaceMembersOperation,
    UpdateWorkspaceMemberPermissionsAgentOperation updateWorkspaceMemberPermissionsOperation,
    RevokeWorkspaceMemberAgentOperation revokeWorkspaceMemberOperation,
    ListWorkspaceBoxesAgentOperation listWorkspaceBoxesOperation,
    GetBoxAgentOperation getBoxOperation,
    CreateBoxAgentOperation createBoxOperation,
    UpdateBoxAgentOperation updateBoxOperation,
    DeleteBoxAgentOperation deleteBoxOperation,
    ListBoxLinksAgentOperation listBoxLinksOperation,
    CreateBoxLinkAgentOperation createBoxLinkOperation,
    UpdateBoxLinkAgentOperation updateBoxLinkOperation,
    DeleteBoxLinkAgentOperation deleteBoxLinkOperation,
    RegenerateBoxLinkAccessCodeAgentOperation regenerateBoxLinkAccessCodeOperation,
    ListBoxMembersAgentOperation listBoxMembersOperation,
    InviteBoxMembersAgentOperation inviteBoxMembersOperation,
    UpdateBoxMemberPermissionsAgentOperation updateBoxMemberPermissionsOperation,
    RevokeBoxMemberAgentOperation revokeBoxMemberOperation,
    ListBoxesAgentOperation listBoxesOperation,
    GetBoxDetailsAgentOperation getBoxDetailsOperation,
    ListBoxContentAgentOperation listBoxContentOperation,
    ReadBoxFileAgentOperation readBoxFileOperation,
    GetBoxFileDownloadLinkAgentOperation getBoxFileDownloadLinkOperation,
    GetBoxBulkDownloadLinkAgentOperation getBoxBulkDownloadLinkOperation,
    SearchBoxAgentOperation searchBoxOperation,
    CreateBoxFolderAgentOperation createBoxFolderOperation,
    CreateBoxFileAgentOperation createBoxFileOperation,
    RenameBoxFileAgentOperation renameBoxFileOperation,
    RenameBoxFolderAgentOperation renameBoxFolderOperation,
    MoveBoxItemsAgentOperation moveBoxItemsOperation,
    DeleteBoxItemsAgentOperation deleteBoxItemsOperation)
{
    public AgentOperationPlan Plan(
        HttpContext httpContext,
        AgentOperation operation)
    {
        switch (operation.ToolName)
        {
            case AgentToolNames.BulkDelete:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<BulkDeleteParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await bulkDeleteOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.DeleteShareLink:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<DeleteShareLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await deleteShareLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.RenameFolder:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<RenameFolderParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await renameFolderOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.RenameFile:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<RenameFileParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await renameFileOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.CreateFolder:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<CreateFolderParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await createFolderOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.MoveItems:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<MoveItemsParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await moveItemsOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.CreateFile:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<CreateFileParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await createFileOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.RenameWorkspace:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<RenameWorkspaceParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await renameWorkspaceOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.CreateShareLink:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<CreateShareLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await createShareLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.UpdateShareLink:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<UpdateShareLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await updateShareLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.CreateWorkspace:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<CreateWorkspaceParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await createWorkspaceOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ReadFile:
                // Idempotent read: the execute flow simply re-reads, so the result is not persisted.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ReadFileParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await readFileOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.GetFile:
                // Idempotent read: the execute flow simply re-reads, so the result is not persisted.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<GetFileParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await getFileOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.GetFileDownloadLink:
                // Idempotent read: each execute mints a fresh link, so the result is not persisted.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<GetFileDownloadLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await getFileDownloadLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ListWorkspaces:
                // Idempotent read with no parameters: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken => await listWorkspacesOperation.Execute(
                        httpContext,
                        cancellationToken));

            case AgentToolNames.ListStorages:
                // Idempotent read with no parameters: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken => await listStoragesOperation.Execute(
                        httpContext,
                        cancellationToken));

            case AgentToolNames.ListShareLinks:
                // Idempotent read: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ListShareLinksParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await listShareLinksOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.GetShareLink:
                // Idempotent read: the execute flow simply re-reads.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<GetShareLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await getShareLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.Search:
                // Idempotent read: the execute flow simply re-searches.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<SearchParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await searchOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ListWorkspaceContent:
                // Idempotent read: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ListWorkspaceContentParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await listWorkspaceContentOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.GetBulkDownloadLink:
                // Idempotent read: each execute mints a fresh link, so the result is not persisted.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<GetBulkDownloadLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await getBulkDownloadLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ListWorkspaceMembers:
                // Idempotent read: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ListWorkspaceMembersParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await listWorkspaceMembersOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.InviteWorkspaceMembers:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<InviteWorkspaceMembersParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await inviteWorkspaceMembersOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.UpdateWorkspaceMemberPermissions:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<UpdateWorkspaceMemberPermissionsParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await updateWorkspaceMemberPermissionsOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.RevokeWorkspaceMember:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<RevokeWorkspaceMemberParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await revokeWorkspaceMemberOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ListWorkspaceBoxes:
                // Idempotent read: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ListWorkspaceBoxesParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await listWorkspaceBoxesOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.GetBox:
                // Idempotent read: the execute flow simply re-reads.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<GetBoxParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await getBoxOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.CreateBox:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<CreateBoxParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await createBoxOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.UpdateBox:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<UpdateBoxParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await updateBoxOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.DeleteBox:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<DeleteBoxParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await deleteBoxOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ListBoxLinks:
                // Idempotent read: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ListBoxLinksParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await listBoxLinksOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.CreateBoxLink:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<CreateBoxLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await createBoxLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.UpdateBoxLink:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<UpdateBoxLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await updateBoxLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.DeleteBoxLink:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<DeleteBoxLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await deleteBoxLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.RegenerateBoxLinkAccessCode:
                // Not idempotent — each run mints a new access code, so the result is persisted and the
                // commit never re-runs it.
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<RegenerateBoxLinkAccessCodeParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await regenerateBoxLinkAccessCodeOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ListBoxMembers:
                // Idempotent read: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ListBoxMembersParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await listBoxMembersOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.InviteBoxMembers:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<InviteBoxMembersParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await inviteBoxMembersOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.UpdateBoxMemberPermissions:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<UpdateBoxMemberPermissionsParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await updateBoxMemberPermissionsOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.RevokeBoxMember:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<RevokeBoxMemberParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await revokeBoxMemberOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ListBoxes:
                // Idempotent read with no parameters: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken => await listBoxesOperation.Execute(
                        httpContext,
                        cancellationToken));

            case AgentToolNames.GetBoxDetails:
                // Idempotent read: the execute flow simply re-reads.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<GetBoxDetailsParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await getBoxDetailsOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ListBoxContent:
                // Idempotent read: the execute flow simply re-lists.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ListBoxContentParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await listBoxContentOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.ReadBoxFile:
                // Idempotent read: the execute flow simply re-reads.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<ReadBoxFileParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await readBoxFileOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.GetBoxFileDownloadLink:
                // Idempotent read: each execute mints a fresh link, so the result is not persisted.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<GetBoxFileDownloadLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await getBoxFileDownloadLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.GetBoxBulkDownloadLink:
                // Idempotent read: each execute mints a fresh link, so the result is not persisted.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<GetBoxBulkDownloadLinkParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await getBoxBulkDownloadLinkOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.SearchBox:
                // Idempotent read: the execute flow simply re-searches.
                return new AgentOperationPlan(
                    PersistsResult: false,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<SearchBoxParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await searchBoxOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.CreateBoxFolder:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<CreateBoxFolderParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await createBoxFolderOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.CreateBoxFile:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<CreateBoxFileParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await createBoxFileOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.RenameBoxFile:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<RenameBoxFileParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await renameBoxFileOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.RenameBoxFolder:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<RenameBoxFolderParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await renameBoxFolderOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.MoveBoxItems:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<MoveBoxItemsParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await moveBoxItemsOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            case AgentToolNames.DeleteBoxItems:
                return new AgentOperationPlan(
                    PersistsResult: true,
                    Execute: async cancellationToken =>
                    {
                        var parameters = Json.Deserialize<DeleteBoxItemsParams>(operation.ParamsJson)
                            ?? throw new McpException("The stored operation parameters were invalid.");

                        return await deleteBoxItemsOperation.Execute(
                            httpContext,
                            parameters,
                            cancellationToken);
                    });

            default:
                throw new McpException($"Operation '{operation.ToolName}' cannot be committed.");
        }
    }
}

public sealed record AgentOperationPlan(
    bool PersistsResult,
    Func<CancellationToken, Task<object>> Execute);
