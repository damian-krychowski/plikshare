using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Folders.Id;
using PlikShare.Mcp.BoxAccess.Search.Contracts;
using PlikShare.Workspaces.SearchFilesTree;
using PlikShare.Workspaces.SearchFilesTree.Contracts;

namespace PlikShare.Mcp.BoxAccess.Search;

/// <summary>
/// The reusable core of search_box: re-validates the agent's box access and searches for files inside the
/// box by name (scoped to the box's subtree), flattening the tree result into a list of files each tagged
/// with the folder that holds it. Called directly by the tool when no approval is required, and by the
/// execute flow once a human has approved the operation. The read is idempotent, so the execute flow simply
/// re-searches.
/// </summary>
public class SearchBoxAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    SearchFilesTreeQuery searchFilesTreeQuery)
{
    public async Task<SearchBoxResponseDto> Execute(
        HttpContext httpContext,
        SearchBoxParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var boxAccess = await httpContext.GetAgentBoxAccess(
            agent,
            boxCache,
            boxAccessCache,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        if (boxAccess.IsOff)
            throw new McpException(
                $"Box '{parameters.BoxExternalId}' is disabled or exposes no folder, so it cannot be searched.");

        var response = searchFilesTreeQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            request: new SearchFilesTreeRequestDto
            {
                Phrase = parameters.Phrase,
                FolderExternalId = string.IsNullOrWhiteSpace(parameters.FolderExternalId)
                    ? null
                    : FolderExtId.Parse(parameters.FolderExternalId)
            },
            userIdentity: boxAccess.UserIdentity,
            boxFolderId: boxAccess.Box.Folder!.Id,
            exposeCreatedAt: false,
            workspaceEncryptionSession: null);

        if (response.TooManyResultsCounter > 0)
            return new SearchBoxResponseDto
            {
                TooManyResults = true,
                MatchCount = response.TooManyResultsCounter,
                Files = []
            };

        var files = response.Files
            .Select(file => new SearchBoxResponseDto.FileDto
            {
                ExternalId = file.ExternalId,
                Name = file.Name,
                Extension = file.Extension,
                SizeInBytes = file.SizeInBytes,
                FolderExternalId = file.FolderIdIndex >= 0 && file.FolderIdIndex < response.FolderExternalIds.Count
                    ? response.FolderExternalIds[file.FolderIdIndex]
                    : null
            })
            .ToList();

        return new SearchBoxResponseDto
        {
            TooManyResults = false,
            MatchCount = files.Count,
            Files = files
        };
    }
}
