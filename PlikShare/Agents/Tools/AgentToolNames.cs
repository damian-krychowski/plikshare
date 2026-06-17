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

    public const string ExecuteOperation = "execute_operation";
    public const string CheckApprovals = "check_approvals";
}
