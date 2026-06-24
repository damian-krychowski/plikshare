#if DEBUG
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Agents.Cache;
using PlikShare.Agents.Id;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Folders.Id;
using PlikShare.Mcp.BoxAccess.BulkDownloadLink;
using PlikShare.Mcp.BoxAccess.Content;
using PlikShare.Mcp.BoxAccess.CreateFile;
using PlikShare.Mcp.BoxAccess.CreateFolder;
using PlikShare.Mcp.BoxAccess.Delete;
using PlikShare.Mcp.BoxAccess.DownloadLink;
using PlikShare.Mcp.BoxAccess.GetDetails;
using PlikShare.Mcp.BoxAccess.MoveItems;
using PlikShare.Mcp.BoxAccess.ReadFile;
using PlikShare.Mcp.BoxAccess.RenameFile;
using PlikShare.Mcp.BoxAccess.RenameFolder;
using PlikShare.Mcp.BoxAccess.Search;
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
using PlikShare.Mcp.Workspaces.Content;
using PlikShare.Mcp.Workspaces.Create;
using PlikShare.Mcp.Workspaces.Rename;

namespace PlikShare.Agents.DevSeed;

/// <summary>
/// Development-only endpoints that drop pending agent operations into the ledger so the approval
/// banner / inbox / details UI can be eyeballed (and approved/denied) without driving a real MCP
/// agent. Reuses real workspaces/folders/files/share-links/boxes the owner owns so the resolved
/// details show actual ids; falls back to placeholder ids when none exist. Compiled out of Release.
/// </summary>
public static class AgentDevSeedEndpoints
{
    public static void MapDevSeedEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/{agentExternalId}/operations/dev-seed", DevSeedOperation)
            .WithName("DevSeedAgentOperation");

        group.MapGet("/{agentExternalId}/operations/dev-seed-all", DevSeedAllOperations)
            .WithName("DevSeedAllAgentOperations");
    }

    // Drops a single pending operation for the given tool (default bulk_delete) into the ledger,
    // so the approval banner/inbox lights up for one specific tool.
    private static async Task<Results<Ok<string>, NotFound<HttpError>>> DevSeedOperation(
        [FromRoute] AgentExtId agentExternalId,
        [FromQuery] string? toolName,
        AgentCache agentCache,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        PlikShareDb plikShareDb,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var agent = await agentCache.TryGetAgent(agentExternalId, cancellationToken);

        if (agent is null)
            return HttpErrors.Agent.NotFound(agentExternalId);

        var tool = string.IsNullOrWhiteSpace(toolName) ? AgentToolNames.BulkDelete : toolName;
        var target = FindDevSeedTarget(plikShareDb, agent.Owner.Id);
        var (workspaceId, paramsJson) = BuildSeedOperation(tool, target);

        var operationId = await operationLedger.CreatePending(
            agentId: agent.Id,
            workspaceId: workspaceId,
            toolName: tool,
            paramsJson: paramsJson,
            expiresAt: clock.UtcNow.AddHours(operationsOptions.ApprovalWindowHours),
            cancellationToken: cancellationToken);

        return TypedResults.Ok(operationId.Value);
    }

    // Seeds one pending operation per catalog tool — the whole inbox lights up so every approval
    // details view can be eyeballed (and approved/denied) at once.
    private static async Task<Results<Ok<string[]>, NotFound<HttpError>>> DevSeedAllOperations(
        [FromRoute] AgentExtId agentExternalId,
        AgentCache agentCache,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        PlikShareDb plikShareDb,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var agent = await agentCache.TryGetAgent(agentExternalId, cancellationToken);

        if (agent is null)
            return HttpErrors.Agent.NotFound(agentExternalId);

        var target = FindDevSeedTarget(plikShareDb, agent.Owner.Id);
        var expiresAt = clock.UtcNow.AddHours(operationsOptions.ApprovalWindowHours);

        var ids = new List<string>();

        foreach (var tool in AgentToolCatalog.Names)
        {
            var (workspaceId, paramsJson) = BuildSeedOperation(tool, target);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspaceId,
                toolName: tool,
                paramsJson: paramsJson,
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            ids.Add(operationId.Value);
        }

        return TypedResults.Ok(ids.ToArray());
    }

    // Builds plausible params for a seeded pending operation. Typed params on purpose — the compiler
    // keeps these in lock-step with each tool's real param shape, so the details resolvers never choke.
    private static (int? WorkspaceId, string ParamsJson) BuildSeedOperation(string toolName, DevSeedTarget? t)
    {
        var ws = t?.WorkspaceExternalId ?? "ws_dev_seed";
        var wsId = t?.WorkspaceId;
        string[] folders = t?.FolderExternalIds is { Length: > 0 } f ? f : ["fo_dev_seed_1", "fo_dev_seed_2"];
        string[] files = t?.FileExternalIds is { Length: > 0 } fi ? fi : ["fi_dev_seed_1"];
        var folder = folders[0];
        var file = files[0];
        var shareLink = t?.ShareLinkExternalId ?? "qsh_dev_seed";
        var storage = t?.StorageExternalId ?? "s_dev_seed";
        var box = t?.BoxExternalId ?? "bx_dev_seed";
        var boxWsId = t?.BoxWorkspaceId;

        return toolName switch
        {
            AgentToolNames.BulkDelete => (wsId, Json.Serialize(new BulkDeleteParams
            {
                WorkspaceExternalId = ws, FolderExternalIds = folders, FileExternalIds = files
            })),

            AgentToolNames.DeleteShareLink => (wsId, Json.Serialize(new DeleteShareLinkParams
            {
                WorkspaceExternalId = ws, ShareLinkExternalId = shareLink
            })),

            AgentToolNames.RenameFolder => (wsId, Json.Serialize(new RenameFolderParams
            {
                WorkspaceExternalId = ws, FolderExternalId = folder, Name = "renamed-folder"
            })),

            AgentToolNames.RenameFile => (wsId, Json.Serialize(new RenameFileParams
            {
                WorkspaceExternalId = ws, FileExternalId = file, Name = "renamed-file"
            })),

            AgentToolNames.RenameWorkspace => (wsId, Json.Serialize(new RenameWorkspaceParams
            {
                WorkspaceExternalId = ws, Name = "renamed-workspace"
            })),

            AgentToolNames.CreateFolder => (wsId, Json.Serialize(new CreateFolderParams
            {
                WorkspaceExternalId = ws, FolderExternalId = FolderExtId.NewId().Value, Name = "new-folder", ParentFolderExternalId = folder
            })),

            AgentToolNames.CreateFile => (wsId, Json.Serialize(new CreateFileParams
            {
                WorkspaceExternalId = ws, Name = "notes.txt", Content = "dev seed content", FolderExternalId = folder, ContentType = "text/plain"
            })),

            AgentToolNames.MoveItems => (wsId, Json.Serialize(new MoveItemsParams
            {
                WorkspaceExternalId = ws, FolderExternalIds = [], FileExternalIds = files, DestinationFolderExternalId = folder
            })),

            AgentToolNames.CreateShareLink => (wsId, Json.Serialize(new CreateShareLinkParams
            {
                WorkspaceExternalId = ws, Name = "dev share", FileExternalIds = files, FolderExternalIds = folders,
                ExcludedFileExternalIds = [], ExcludedFolderExternalIds = [], ExpiresAt = null, MaxDownloads = null,
                PasswordHashBase64 = null, PasswordSalt = null
            })),

            AgentToolNames.UpdateShareLink => (wsId, Json.Serialize(new UpdateShareLinkParams
            {
                WorkspaceExternalId = ws, ShareLinkExternalId = shareLink, UpdateName = true, Name = "updated share",
                UpdateExpiration = false, ExpiresAt = null, UpdateMaxDownloads = false, MaxDownloads = null,
                UpdatePassword = false, PasswordHashBase64 = null, PasswordSalt = null, PasswordSet = false
            })),

            AgentToolNames.CreateWorkspace => (null, Json.Serialize(new CreateWorkspaceParams
            {
                Name = "dev workspace", StorageExternalId = storage
            })),

            AgentToolNames.GetFile => (wsId, Json.Serialize(new GetFileParams
            {
                FileExternalId = file
            })),

            AgentToolNames.ReadFile => (wsId, Json.Serialize(new ReadFileParams
            {
                FileExternalId = file, Offset = 0, MaxBytes = null
            })),

            AgentToolNames.GetFileDownloadLink => (wsId, Json.Serialize(new GetFileDownloadLinkParams
            {
                FileExternalId = file, ExpiresInMinutes = 15
            })),

            AgentToolNames.GetBulkDownloadLink => (wsId, Json.Serialize(new GetBulkDownloadLinkParams
            {
                WorkspaceExternalId = ws, FileExternalIds = files, FolderExternalIds = folders,
                ExcludedFileExternalIds = [], ExcludedFolderExternalIds = [], ExpiresInMinutes = 15
            })),

            AgentToolNames.ListWorkspaceContent => (wsId, Json.Serialize(new ListWorkspaceContentParams
            {
                WorkspaceExternalId = ws, FolderExternalId = folder, Type = null, Cursor = null, Limit = null
            })),

            AgentToolNames.ListShareLinks => (wsId, Json.Serialize(new ListShareLinksParams
            {
                WorkspaceExternalId = ws
            })),

            AgentToolNames.GetShareLink => (wsId, Json.Serialize(new GetShareLinkParams
            {
                WorkspaceExternalId = ws, ShareLinkExternalId = shareLink
            })),

            AgentToolNames.Search => (null, Json.Serialize(new SearchParams
            {
                WorkspaceIds = null, FolderIds = null, ExcludeWorkspaceIds = null, ExcludeFolderIds = null,
                Types = ["file"], NameContains = ["report"], Extensions = null, ContentTypes = null,
                CreatedAfter = null, CreatedBefore = null, SizeMin = null, SizeMax = null, Cursor = null, Limit = null
            })),

            AgentToolNames.GetBoxDetails => (boxWsId, Json.Serialize(new GetBoxDetailsParams
            {
                BoxExternalId = box
            })),

            AgentToolNames.ListBoxContent => (boxWsId, Json.Serialize(new ListBoxContentParams
            {
                BoxExternalId = box, FolderExternalId = folder
            })),

            AgentToolNames.ReadBoxFile => (boxWsId, Json.Serialize(new ReadBoxFileParams
            {
                BoxExternalId = box, FileExternalId = file, Offset = 0, MaxBytes = null
            })),

            AgentToolNames.GetBoxFileDownloadLink => (boxWsId, Json.Serialize(new GetBoxFileDownloadLinkParams
            {
                BoxExternalId = box, FileExternalId = file, ExpiresInMinutes = 15
            })),

            AgentToolNames.GetBoxBulkDownloadLink => (boxWsId, Json.Serialize(new GetBoxBulkDownloadLinkParams
            {
                BoxExternalId = box, FileExternalIds = files, FolderExternalIds = folders,
                ExcludedFileExternalIds = [], ExcludedFolderExternalIds = [], ExpiresInMinutes = 15
            })),

            AgentToolNames.SearchBox => (boxWsId, Json.Serialize(new SearchBoxParams
            {
                BoxExternalId = box, Phrase = "report", FolderExternalId = folder
            })),

            AgentToolNames.CreateBoxFolder => (boxWsId, Json.Serialize(new CreateBoxFolderParams
            {
                BoxExternalId = box, FolderExternalId = FolderExtId.NewId().Value, Name = "new-folder", ParentFolderExternalId = folder
            })),

            AgentToolNames.CreateBoxFile => (boxWsId, Json.Serialize(new CreateBoxFileParams
            {
                BoxExternalId = box, Name = "notes.txt", Content = "dev seed content", FolderExternalId = folder, ContentType = "text/plain"
            })),

            AgentToolNames.RenameBoxFile => (boxWsId, Json.Serialize(new RenameBoxFileParams
            {
                BoxExternalId = box, FileExternalId = file, Name = "renamed-file"
            })),

            AgentToolNames.RenameBoxFolder => (boxWsId, Json.Serialize(new RenameBoxFolderParams
            {
                BoxExternalId = box, FolderExternalId = folder, Name = "renamed-folder"
            })),

            AgentToolNames.MoveBoxItems => (boxWsId, Json.Serialize(new MoveBoxItemsParams
            {
                BoxExternalId = box, FolderExternalIds = [], FileExternalIds = files, DestinationFolderExternalId = folder
            })),

            AgentToolNames.DeleteBoxItems => (boxWsId, Json.Serialize(new DeleteBoxItemsParams
            {
                BoxExternalId = box, FileExternalIds = files, FolderExternalIds = folders
            })),

            _ => (null, "{}")
        };
    }

    private static DevSeedTarget? FindDevSeedTarget(PlikShareDb plikShareDb, int ownerUserId)
    {
        using var connection = plikShareDb.OpenConnection();

        var workspace = connection
            .OneRowCmd(
                sql: """
                     SELECT w_id, w_external_id
                     FROM w_workspaces
                     WHERE w_owner_id = $ownerId
                       AND w_is_being_deleted = FALSE
                       AND (EXISTS (SELECT 1 FROM fo_folders WHERE fo_workspace_id = w_id AND fo_is_being_deleted = FALSE)
                            OR EXISTS (SELECT 1 FROM fi_files WHERE fi_workspace_id = w_id AND fi_deleted_at IS NULL AND fi_is_upload_completed = TRUE))
                     ORDER BY w_id DESC
                     LIMIT 1
                     """,
                readRowFunc: reader => new { Id = reader.GetInt32(0), ExternalId = reader.GetString(1) })
            .WithParameter("$ownerId", ownerUserId)
            .Execute();

        if (workspace.IsEmpty)
            return null;

        var workspaceId = workspace.Value.Id;

        var folders = connection
            .Cmd(
                sql: """
                     SELECT fo_external_id FROM fo_folders
                     WHERE fo_workspace_id = $workspaceId AND fo_is_being_deleted = FALSE
                     ORDER BY fo_id DESC LIMIT 3
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        var files = connection
            .Cmd(
                sql: """
                     SELECT fi_external_id FROM fi_files
                     WHERE fi_workspace_id = $workspaceId AND fi_deleted_at IS NULL
                       AND fi_is_upload_completed = TRUE AND fi_parent_file_id IS NULL
                     ORDER BY fi_id DESC LIMIT 3
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        var shareLink = connection
            .OneRowCmd(
                sql: """
                     SELECT qsh_external_id FROM qsh_quick_shares
                     WHERE qsh_workspace_id = $workspaceId
                     ORDER BY qsh_id DESC LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        var storage = connection
            .OneRowCmd(
                sql: """
                     SELECT s.s_external_id
                     FROM s_storages AS s
                     INNER JOIN w_workspaces AS w ON w.w_storage_id = s.s_id
                     WHERE w.w_id = $workspaceId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        // Boxes live one-per-workspace and need an attached folder to expose content, so this is a
        // separate lookup across all the owner's workspaces — the file/folder target above may sit in
        // a workspace that has no box at all.
        var boxRow = connection
            .OneRowCmd(
                sql: """
                     SELECT bo.bo_external_id, bo.bo_workspace_id
                     FROM bo_boxes AS bo
                     INNER JOIN w_workspaces AS w ON w.w_id = bo.bo_workspace_id
                     WHERE w.w_owner_id = $ownerId
                       AND bo.bo_is_being_deleted = FALSE
                       AND bo.bo_folder_id IS NOT NULL
                     ORDER BY bo.bo_id DESC
                     LIMIT 1
                     """,
                readRowFunc: reader => new { ExternalId = reader.GetString(0), WorkspaceId = reader.GetInt32(1) })
            .WithParameter("$ownerId", ownerUserId)
            .Execute();

        return new DevSeedTarget(
            WorkspaceId: workspaceId,
            WorkspaceExternalId: workspace.Value.ExternalId,
            FolderExternalIds: folders.ToArray(),
            FileExternalIds: files.ToArray(),
            ShareLinkExternalId: shareLink.IsEmpty ? null : shareLink.Value,
            StorageExternalId: storage.IsEmpty ? null : storage.Value,
            BoxExternalId: boxRow.IsEmpty ? null : boxRow.Value.ExternalId,
            BoxWorkspaceId: boxRow.IsEmpty ? null : boxRow.Value.WorkspaceId);
    }

    private sealed record DevSeedTarget(
        int WorkspaceId,
        string WorkspaceExternalId,
        string[] FolderExternalIds,
        string[] FileExternalIds,
        string? ShareLinkExternalId,
        string? StorageExternalId,
        string? BoxExternalId,
        int? BoxWorkspaceId);
}
#endif
