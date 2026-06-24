namespace PlikShare.Agents.Tools;

/// <summary>
/// The single source of truth for MCP tool names. Every place that needs a tool name — the
/// <see cref="AgentToolCatalog"/>, each tool's <c>[McpServerTool(Name = ...)]</c>, the operation
/// dispatch/details switches — references a const here, never a copy-pasted string literal.
/// </summary>
public static class AgentToolNames
{
    public const string ListWorkspaces = "list_workspaces";
    public const string ListStorages = "list_storages";
    public const string CreateWorkspace = "create_workspace";

    public const string GetFile = "get_file";
    public const string ReadFile = "read_file";
    public const string Search = "search";
    public const string GetFileDownloadLink = "get_file_download_link";

    public const string ListWorkspaceContent = "list_workspace_content";
    public const string ListShareLinks = "list_share_links";
    public const string GetShareLink = "get_share_link";
    public const string GetBulkDownloadLink = "get_bulk_download_link";

    public const string RenameWorkspace = "rename_workspace";
    public const string CreateFile = "create_file";
    public const string RenameFile = "rename_file";
    public const string CreateFolder = "create_folder";
    public const string RenameFolder = "rename_folder";
    public const string MoveItems = "move_items";
    public const string CreateShareLink = "create_share_link";
    public const string UpdateShareLink = "update_share_link";

    public const string BulkDelete = "bulk_delete";
    public const string DeleteShareLink = "delete_share_link";

    public const string ListWorkspaceMembers = "list_workspace_members";
    public const string InviteWorkspaceMembers = "invite_workspace_members";
    public const string UpdateWorkspaceMemberPermissions = "update_workspace_member_permissions";
    public const string RevokeWorkspaceMember = "revoke_workspace_member";

    public const string ListWorkspaceBoxes = "list_workspace_boxes";
    public const string GetBox = "get_box";
    public const string CreateBox = "create_box";
    public const string UpdateBox = "update_box";
    public const string DeleteBox = "delete_box";

    public const string ListBoxLinks = "list_box_links";
    public const string CreateBoxLink = "create_box_link";
    public const string UpdateBoxLink = "update_box_link";
    public const string DeleteBoxLink = "delete_box_link";
    public const string RegenerateBoxLinkAccessCode = "regenerate_box_link_access_code";

    public const string ListBoxMembers = "list_box_members";
    public const string InviteBoxMembers = "invite_box_members";
    public const string UpdateBoxMemberPermissions = "update_box_member_permissions";
    public const string RevokeBoxMember = "revoke_box_member";

    // Box-access tools: the agent acts as a consumer inside a box it was granted access to
    // (ba_box_agents), scoped to the box's folder. Gated by box access + per-box tool config.
    // list_boxes mirrors list_workspaces — it lists the boxes shared directly with the agent.
    public const string ListBoxes = "list_boxes";
    public const string GetBoxDetails = "get_box_details";
    public const string ListBoxContent = "list_box_content";
    public const string ReadBoxFile = "read_box_file";
    public const string GetBoxFileDownloadLink = "get_box_file_download_link";
    public const string GetBoxBulkDownloadLink = "get_box_bulk_download_link";
    public const string SearchBox = "search_box";
    public const string CreateBoxFolder = "create_box_folder";
    public const string CreateBoxFile = "create_box_file";
    public const string RenameBoxFile = "rename_box_file";
    public const string RenameBoxFolder = "rename_box_folder";
    public const string MoveBoxItems = "move_box_items";
    public const string DeleteBoxItems = "delete_box_items";

    public const string ExecuteOperation = "execute_operation";
    public const string CheckApprovals = "check_approvals";
}
