using PlikShare.Antiforgery;
using PlikShare.Core.Authorization;
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
        builder.Services.AddSingleton<ListBoxesAgentOperation>();
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
        builder.Services.AddSingleton<ListBoxesOperationDetailsResolver>();
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

        builder
            .Services
            .AddMcpServer()
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
            .WithTools<ListBoxesTool>()
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
