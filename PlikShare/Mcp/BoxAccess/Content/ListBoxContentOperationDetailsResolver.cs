using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxAccess.Content.Contracts;

namespace PlikShare.Mcp.BoxAccess.Content;

/// <summary>
/// Resolves a list_box_content operation's stored ids into the box name and the folder being listed
/// (its name, or the box root), so a human reviewing the approval sees what gets listed.
/// </summary>
public class ListBoxContentOperationDetailsResolver(
    BoxApprovalNameResolver boxNameResolver,
    AuditLogService auditLogService)
{
    public ListBoxContentOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<ListBoxContentParams>(operation.ParamsJson)
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

        return new ListBoxContentOperationDetails
        {
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxNameResolver.GetBoxName(parameters.BoxExternalId),
            FolderExternalId = parameters.FolderExternalId,
            FolderName = folderName
        };
    }
}
