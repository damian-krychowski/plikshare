using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Utils;
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
    GetBulkDownloadLinkAgentOperation getBulkDownloadLinkOperation)
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

            default:
                throw new McpException($"Operation '{operation.ToolName}' cannot be committed.");
        }
    }
}

public sealed record AgentOperationPlan(
    bool PersistsResult,
    Func<CancellationToken, Task<object>> Execute);
