using PlikShare.Agents.Tools;
using PlikShare.Antiforgery;
using PlikShare.Core.Authorization;
using PlikShare.Mcp.BoxAccess;
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
using PlikShare.Mcp.Files;
using PlikShare.Mcp.Files.BulkDownloadLink;
using PlikShare.Mcp.Files.Create;
using PlikShare.Mcp.Files.DownloadLink;
using PlikShare.Mcp.Files.Get;
using PlikShare.Mcp.Files.Read;
using PlikShare.Mcp.Files.Rename;
using PlikShare.Mcp.Folders.Create;
using PlikShare.Mcp.Folders.Rename;
using PlikShare.Mcp.MoveItems;
using PlikShare.Mcp.Operations;
using PlikShare.Mcp.Search;
using PlikShare.Mcp.Storages.List;
using PlikShare.Mcp.ShareLinks.Create;
using PlikShare.Mcp.ShareLinks.Delete;
using PlikShare.Mcp.ShareLinks.Get;
using PlikShare.Mcp.ShareLinks.List;
using PlikShare.Mcp.ShareLinks.Update;
using PlikShare.Mcp.Workspaces.Content;
using PlikShare.Mcp.Workspaces.Create;
using PlikShare.Mcp.Workspaces.List;
using PlikShare.Mcp.Workspaces.Members.Invite;
using PlikShare.Mcp.Workspaces.Members.List;
using PlikShare.Mcp.Workspaces.Members.Revoke;
using PlikShare.Mcp.Workspaces.Members.UpdatePermissions;
using PlikShare.Mcp.Workspaces.Rename;

namespace PlikShare.Mcp;

public static class McpStartupExtensions
{
    public static void AddPlikShareMcp(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddSingleton<BulkDeleteAgentOperation>();
        builder.Services.AddSingleton<DeleteShareLinkAgentOperation>();
        builder.Services.AddSingleton<RenameFolderAgentOperation>();
        builder.Services.AddSingleton<RenameFileAgentOperation>();
        builder.Services.AddSingleton<CreateFolderAgentOperation>();
        builder.Services.AddSingleton<MoveItemsAgentOperation>();
        builder.Services.AddSingleton<CreateFileAgentOperation>();
        builder.Services.AddSingleton<RenameWorkspaceAgentOperation>();
        builder.Services.AddSingleton<CreateShareLinkAgentOperation>();
        builder.Services.AddSingleton<UpdateShareLinkAgentOperation>();
        builder.Services.AddSingleton<CreateWorkspaceAgentOperation>();
        builder.Services.AddSingleton<ReadFileAgentOperation>();
        builder.Services.AddSingleton<GetFileAgentOperation>();
        builder.Services.AddSingleton<GetFileDownloadLinkAgentOperation>();
        builder.Services.AddSingleton<ListWorkspacesAgentOperation>();
        builder.Services.AddSingleton<ListStoragesAgentOperation>();
        builder.Services.AddSingleton<ListShareLinksAgentOperation>();
        builder.Services.AddSingleton<GetShareLinkAgentOperation>();
        builder.Services.AddSingleton<SearchAgentOperation>();
        builder.Services.AddSingleton<ListWorkspaceContentAgentOperation>();
        builder.Services.AddSingleton<GetBulkDownloadLinkAgentOperation>();
        builder.Services.AddSingleton<ListWorkspaceMembersAgentOperation>();
        builder.Services.AddSingleton<InviteWorkspaceMembersAgentOperation>();
        builder.Services.AddSingleton<UpdateWorkspaceMemberPermissionsAgentOperation>();
        builder.Services.AddSingleton<RevokeWorkspaceMemberAgentOperation>();
        builder.Services.AddSingleton<ListWorkspaceBoxesAgentOperation>();
        builder.Services.AddSingleton<GetBoxAgentOperation>();
        builder.Services.AddSingleton<CreateBoxAgentOperation>();
        builder.Services.AddSingleton<UpdateBoxAgentOperation>();
        builder.Services.AddSingleton<DeleteBoxAgentOperation>();
        builder.Services.AddSingleton<ListBoxLinksAgentOperation>();
        builder.Services.AddSingleton<CreateBoxLinkAgentOperation>();
        builder.Services.AddSingleton<UpdateBoxLinkAgentOperation>();
        builder.Services.AddSingleton<DeleteBoxLinkAgentOperation>();
        builder.Services.AddSingleton<RegenerateBoxLinkAccessCodeAgentOperation>();
        builder.Services.AddSingleton<ListBoxMembersAgentOperation>();
        builder.Services.AddSingleton<InviteBoxMembersAgentOperation>();
        builder.Services.AddSingleton<UpdateBoxMemberPermissionsAgentOperation>();
        builder.Services.AddSingleton<RevokeBoxMemberAgentOperation>();
        builder.Services.AddSingleton<ListBoxesAgentOperation>();
        builder.Services.AddSingleton<GetBoxDetailsAgentOperation>();
        builder.Services.AddSingleton<ListBoxContentAgentOperation>();
        builder.Services.AddSingleton<ReadBoxFileAgentOperation>();
        builder.Services.AddSingleton<GetBoxFileDownloadLinkAgentOperation>();
        builder.Services.AddSingleton<GetBoxBulkDownloadLinkAgentOperation>();
        builder.Services.AddSingleton<SearchBoxAgentOperation>();
        builder.Services.AddSingleton<CreateBoxFolderAgentOperation>();
        builder.Services.AddSingleton<CreateBoxFileAgentOperation>();
        builder.Services.AddSingleton<RenameBoxFileAgentOperation>();
        builder.Services.AddSingleton<RenameBoxFolderAgentOperation>();
        builder.Services.AddSingleton<MoveBoxItemsAgentOperation>();
        builder.Services.AddSingleton<DeleteBoxItemsAgentOperation>();
        builder.Services.AddSingleton<AgentFileWorkspaceLocator>();
        builder.Services.AddSingleton<AgentSearchScopeResolver>();
        builder.Services.AddSingleton<AgentOperationDispatcher>();

        builder.Services.AddSingleton<BulkDeleteOperationDetailsResolver>();
        builder.Services.AddSingleton<DeleteShareLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<RenameFolderOperationDetailsResolver>();
        builder.Services.AddSingleton<RenameFileOperationDetailsResolver>();
        builder.Services.AddSingleton<CreateFolderOperationDetailsResolver>();
        builder.Services.AddSingleton<MoveItemsOperationDetailsResolver>();
        builder.Services.AddSingleton<CreateFileOperationDetailsResolver>();
        builder.Services.AddSingleton<RenameWorkspaceOperationDetailsResolver>();
        builder.Services.AddSingleton<CreateShareLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<UpdateShareLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<CreateWorkspaceOperationDetailsResolver>();
        builder.Services.AddSingleton<ReadFileOperationDetailsResolver>();
        builder.Services.AddSingleton<GetFileOperationDetailsResolver>();
        builder.Services.AddSingleton<GetFileDownloadLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<ListWorkspacesOperationDetailsResolver>();
        builder.Services.AddSingleton<ListStoragesOperationDetailsResolver>();
        builder.Services.AddSingleton<ListShareLinksOperationDetailsResolver>();
        builder.Services.AddSingleton<GetShareLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<SearchOperationDetailsResolver>();
        builder.Services.AddSingleton<ListWorkspaceContentOperationDetailsResolver>();
        builder.Services.AddSingleton<GetBulkDownloadLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<ListWorkspaceMembersOperationDetailsResolver>();
        builder.Services.AddSingleton<InviteWorkspaceMembersOperationDetailsResolver>();
        builder.Services.AddSingleton<UpdateWorkspaceMemberPermissionsOperationDetailsResolver>();
        builder.Services.AddSingleton<RevokeWorkspaceMemberOperationDetailsResolver>();
        builder.Services.AddSingleton<ListWorkspaceBoxesOperationDetailsResolver>();
        builder.Services.AddSingleton<GetBoxOperationDetailsResolver>();
        builder.Services.AddSingleton<CreateBoxOperationDetailsResolver>();
        builder.Services.AddSingleton<UpdateBoxOperationDetailsResolver>();
        builder.Services.AddSingleton<DeleteBoxOperationDetailsResolver>();
        builder.Services.AddSingleton<ListBoxLinksOperationDetailsResolver>();
        builder.Services.AddSingleton<CreateBoxLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<UpdateBoxLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<DeleteBoxLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<RegenerateBoxLinkAccessCodeOperationDetailsResolver>();
        builder.Services.AddSingleton<ListBoxMembersOperationDetailsResolver>();
        builder.Services.AddSingleton<InviteBoxMembersOperationDetailsResolver>();
        builder.Services.AddSingleton<UpdateBoxMemberPermissionsOperationDetailsResolver>();
        builder.Services.AddSingleton<RevokeBoxMemberOperationDetailsResolver>();
        builder.Services.AddSingleton<BoxApprovalNameResolver>();
        builder.Services.AddSingleton<ListBoxesOperationDetailsResolver>();
        builder.Services.AddSingleton<GetBoxDetailsOperationDetailsResolver>();
        builder.Services.AddSingleton<ListBoxContentOperationDetailsResolver>();
        builder.Services.AddSingleton<ReadBoxFileOperationDetailsResolver>();
        builder.Services.AddSingleton<GetBoxFileDownloadLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<GetBoxBulkDownloadLinkOperationDetailsResolver>();
        builder.Services.AddSingleton<SearchBoxOperationDetailsResolver>();
        builder.Services.AddSingleton<CreateBoxFolderOperationDetailsResolver>();
        builder.Services.AddSingleton<CreateBoxFileOperationDetailsResolver>();
        builder.Services.AddSingleton<RenameBoxFileOperationDetailsResolver>();
        builder.Services.AddSingleton<RenameBoxFolderOperationDetailsResolver>();
        builder.Services.AddSingleton<MoveBoxItemsOperationDetailsResolver>();
        builder.Services.AddSingleton<DeleteBoxItemsOperationDetailsResolver>();

        builder
            .Services
            .AddMcpServer(options =>
            {
                options.ServerInstructions =
                    "PlikShare lets you manage files on behalf of users through these tools. You can reach " +
                    "files through two independent access surfaces, and you may have either or both: " +
                    $"(1) workspaces you are a member of - call {AgentToolNames.ListWorkspaces}, then use the workspace tools; " +
                    $"(2) individual boxes shared directly with you - call {AgentToolNames.ListBoxes}, then use the box-access " +
                    $"tools ({AgentToolNames.GetBoxDetails}, {AgentToolNames.ListBoxContent}, {AgentToolNames.ReadBoxFile} and the other box tools). " +
                    $"At the start of a task, check BOTH {AgentToolNames.ListWorkspaces} and {AgentToolNames.ListBoxes}; an empty result " +
                    "from one does not mean you have no access, because the two surfaces are separate. Some tools require " +
                    "human approval before they run - when a call returns status 'waits_for_approval', poll " +
                    $"{AgentToolNames.CheckApprovals} and then call {AgentToolNames.ExecuteOperation} once it is approved.";
            })
            .WithHttpTransport()
            .WithTools<ListWorkspacesTool>()
            .WithTools<ListStoragesTool>()
            .WithTools<CreateWorkspaceTool>()
            .WithTools<RenameWorkspaceTool>()
            .WithTools<ListWorkspaceContentTool>()
            .WithTools<SearchTool>()
            .WithTools<GetFileTool>()
            .WithTools<ReadFileTool>()
            .WithTools<GetFileDownloadLinkTool>()
            .WithTools<GetBulkDownloadLinkTool>()
            .WithTools<CreateFileTool>()
            .WithTools<RenameFileTool>()
            .WithTools<CreateFolderTool>()
            .WithTools<RenameFolderTool>()
            .WithTools<MoveItemsTool>()
            .WithTools<BulkDeleteTool>()
            .WithTools<CreateShareLinkTool>()
            .WithTools<ListShareLinksTool>()
            .WithTools<GetShareLinkTool>()
            .WithTools<UpdateShareLinkTool>()
            .WithTools<DeleteShareLinkTool>()
            .WithTools<ListWorkspaceMembersTool>()
            .WithTools<InviteWorkspaceMembersTool>()
            .WithTools<UpdateWorkspaceMemberPermissionsTool>()
            .WithTools<RevokeWorkspaceMemberTool>()
            .WithTools<ListWorkspaceBoxesTool>()
            .WithTools<GetBoxTool>()
            .WithTools<CreateBoxTool>()
            .WithTools<UpdateBoxTool>()
            .WithTools<DeleteBoxTool>()
            .WithTools<ListBoxLinksTool>()
            .WithTools<CreateBoxLinkTool>()
            .WithTools<UpdateBoxLinkTool>()
            .WithTools<DeleteBoxLinkTool>()
            .WithTools<RegenerateBoxLinkAccessCodeTool>()
            .WithTools<ListBoxMembersTool>()
            .WithTools<InviteBoxMembersTool>()
            .WithTools<UpdateBoxMemberPermissionsTool>()
            .WithTools<RevokeBoxMemberTool>()
            .WithTools<ListBoxesTool>()
            .WithTools<GetBoxDetailsTool>()
            .WithTools<ListBoxContentTool>()
            .WithTools<ReadBoxFileTool>()
            .WithTools<GetBoxFileDownloadLinkTool>()
            .WithTools<GetBoxBulkDownloadLinkTool>()
            .WithTools<SearchBoxTool>()
            .WithTools<CreateBoxFolderTool>()
            .WithTools<CreateBoxFileTool>()
            .WithTools<RenameBoxFileTool>()
            .WithTools<RenameBoxFolderTool>()
            .WithTools<MoveBoxItemsTool>()
            .WithTools<DeleteBoxItemsTool>()
            .WithTools<ExecuteOperationTool>()
            .WithTools<CheckApprovalsTool>();
    }

    public static void MapPlikShareMcp(this WebApplication app)
    {
        app.MapMcp("/mcp")
            .RequireAuthorization(AuthPolicy.AgentToken)
            .WithMetadata(new DisableAutoAntiforgeryCheck());
    }
}
