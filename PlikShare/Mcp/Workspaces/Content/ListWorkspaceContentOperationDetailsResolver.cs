using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Workspaces.Content.Contracts;

namespace PlikShare.Mcp.Workspaces.Content;

/// <summary>
/// Resolves a list_workspace_content operation's stored ids into the folder being listed (its name, or
/// the workspace root) and the type filter, so a human reviewing the approval sees what gets listed.
/// </summary>
public class ListWorkspaceContentOperationDetailsResolver(
    AuditLogService auditLogService)
{
    public ListWorkspaceContentOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<ListWorkspaceContentParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        string? folderName = null;

        if (!string.IsNullOrWhiteSpace(parameters.FolderExternalId))
        {
            var items = auditLogService.GetBulkItemsContext(
                folderExternalIds: [parameters.FolderExternalId!],
                fileExternalIds: [],
                fileUploadExternalIds: []);

            folderName = items.Folders.FirstOrDefault()?.Name.Encoded;
        }

        return new ListWorkspaceContentOperationDetails
        {
            FolderExternalId = parameters.FolderExternalId,
            FolderName = folderName,
            Type = parameters.Type
        };
    }
}
