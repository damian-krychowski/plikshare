using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxAccess.Search.Contracts;

namespace PlikShare.Mcp.BoxAccess.Search;

/// <summary>
/// Resolves a search_box operation's stored ids into the box name, the search phrase and the folder it
/// is scoped to (its name, or the whole box), so a human reviewing the approval sees what gets searched.
/// </summary>
public class SearchBoxOperationDetailsResolver(
    BoxApprovalNameResolver boxNameResolver,
    AuditLogService auditLogService)
{
    public SearchBoxOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<SearchBoxParams>(operation.ParamsJson)
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

        return new SearchBoxOperationDetails
        {
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxNameResolver.GetBoxName(parameters.BoxExternalId),
            Phrase = parameters.Phrase,
            FolderExternalId = parameters.FolderExternalId,
            FolderName = folderName
        };
    }
}
