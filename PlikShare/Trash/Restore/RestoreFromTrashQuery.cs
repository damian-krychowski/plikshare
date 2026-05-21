using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Trash.Restore.Contracts;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Trash.Restore;

/// <summary>
/// Restores soft-deleted files back into the live workspace tree. For each item the caller
/// picks one of two modes:
/// <list type="bullet">
///   <item><c>original-path</c> — walk the file's <c>fi_original_folder_path</c> snapshot
///         segment by segment, ID-first (use folder if it still exists, even moved/renamed),
///         name-fallback if not, create if neither. Result: the file lands as close to its
///         original location as the current tree allows.</item>
///   <item><c>chosen-folder</c> — put the file directly in the caller-specified folder. The
///         snapshot is ignored.</item>
/// </list>
/// Name collisions (a live file with the same name+extension already exists at the destination)
/// are resolved by suffixing the restored name with " (restored)" or " (restored 2)", etc.
/// Child files (parent_file_id IS NOT NULL) are un-trashed alongside their parent in the same
/// pass — they never restore independently.
/// </summary>
public class RestoreFromTrashQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<RestoreFromTrashResponseDto> Execute(
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        RestoreFromTrashRequestDto request,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                userIdentity: userIdentity,
                request: request,
                workspaceEncryptionSession: workspaceEncryptionSession),
            cancellationToken: cancellationToken);
    }

    private RestoreFromTrashResponseDto ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        RestoreFromTrashRequestDto request,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        var results = new List<RestoreItemResultDto>(
            request.Items.Count);

        try
        {
            foreach (var item in request.Items)
            {
                var result = RestoreOne(
                    item: item,
                    workspace: workspace,
                    userIdentity: userIdentity,
                    workspaceEncryptionSession: workspaceEncryptionSession,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                results.Add(result);
            }

            transaction.Commit();

            Log.Information(
                "Trash restore in Workspace#{WorkspaceId}: {Restored}/{Total} items restored",
                workspace.Id,
                results.Count(r => r.Status == RestoreStatus.Restored),
                results.Count);

            return new RestoreFromTrashResponseDto { Results = results };
        }
        catch (Exception e)
        {
            transaction.Rollback();
            Log.Error(e, "Restore from trash failed for Workspace#{WorkspaceId}", workspace.Id);
            throw;
        }
    }

    private RestoreItemResultDto RestoreOne(
        RestoreItemDto item,
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var trashed = LoadTrashedFile(
            workspace.Id,
            item.FileExternalId,
            workspaceEncryptionSession,
            dbWriteContext,
            transaction);

        if (trashed is null)
            return new RestoreItemResultDto
            {
                FileExternalId = item.FileExternalId,
                Status = RestoreStatus.NotFound
            };

        int? destinationFolderId;

        if (item.Mode == RestoreMode.ChosenFolder)
        {
            var resolved = ResolveChosenFolder(
                workspace.Id,
                item.TargetFolderExternalId,
                dbWriteContext,
                transaction);

            if (resolved is null)
                return new RestoreItemResultDto
                {
                    FileExternalId = item.FileExternalId,
                    Status = RestoreStatus.DestinationInvalid
                };

            destinationFolderId = resolved.Value.Id;
        }
        else if (item.Mode == RestoreMode.OriginalPath)
        {
            destinationFolderId = WalkOriginalPath(
                workspace: workspace,
                userIdentity: userIdentity,
                path: trashed.Path,
                workspaceEncryptionSession: workspaceEncryptionSession,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(item.Mode),
                item.Mode,
                $"Unhandled {nameof(RestoreMode)} value.");
        }

        var finalName = ResolveUniqueName(
            workspaceId: workspace.Id,
            destinationFolderId: destinationFolderId,
            desiredName: trashed.Name,
            extension: trashed.Extension,
            workspaceEncryptionSession: workspaceEncryptionSession,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        UpdateFileBack(
            fileId: trashed.Id,
            folderId: destinationFolderId,
            finalName: finalName,
            workspaceEncryptionSession: workspaceEncryptionSession,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        // Children (artifacts: thumbnails, OCR, AI metadata) ride along with the parent —
        // they got soft-deleted in the same transaction as the parent and now un-trash together.
        // They keep fi_folder_id NULL because that's never meaningful for child files anyway
        // (parent's fi_folder_id is the authoritative location).
        UnTrashChildren(
            parentFileId: trashed.Id,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        return new RestoreItemResultDto
        {
            FileExternalId = item.FileExternalId,
            Status = RestoreStatus.Restored
        };
    }

    private static TrashedFile? LoadTrashedFile(
        int workspaceId,
        FileExtId fileExternalId,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT fi_id, fi_name, fi_extension, fi_original_folder_path
                     FROM fi_files
                     WHERE fi_workspace_id = $workspaceId
                       AND fi_external_id = $externalId
                       AND fi_deleted_at IS NOT NULL
                       AND fi_parent_file_id IS NULL
                     LIMIT 1
                     """,
                readRowFunc: reader => new TrashedFile(
                    Id: reader.GetInt32(0),
                    Name: reader.DecodeEncryptableString(1, workspaceEncryptionSession),
                    Extension: reader.DecodeEncryptableString(2, workspaceEncryptionSession),
                    Path: reader.GetFromJsonOrNull<List<OriginalFolderPathSegment>>(3)
                        ?.Select(s => s with { Name = workspaceEncryptionSession.DecodeEncryptableMetadata(s.Name) })
                        .ToList()),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$externalId", fileExternalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static ChosenFolder? ResolveChosenFolder(
        int workspaceId,
        FolderExtId? folderExternalId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderExternalId is null)
        {
            // null target = workspace root, valid choice
            return new ChosenFolder(Id: null);
        }

        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT fo_id
                     FROM fo_folders
                     WHERE fo_workspace_id = $workspaceId
                       AND fo_external_id = $externalId
                       AND fo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => new ChosenFolder(Id: reader.GetInt32(0)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$externalId", folderExternalId.Value.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    // Returns the destination folder id the file should land in — null meaning workspace root.
    private int? WalkOriginalPath(
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        IReadOnlyList<OriginalFolderPathSegment>? path,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (path is null || path.Count == 0)
        {
            // Snapshot missing → fall back to workspace root.
            return null;
        }

        int? currentParentId = null;

        foreach (var segment in path)
        {
            var resolved = ResolveSegmentByIdThenName(
                workspaceId: workspace.Id,
                segmentId: segment.FolderId,
                plainName: segment.Name,
                currentParentId: currentParentId,
                workspaceEncryptionSession: workspaceEncryptionSession,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            currentParentId = resolved
                ?? CreateFolder(
                    workspaceId: workspace.Id,
                    parentId: currentParentId,
                    plainName: segment.Name,
                    userIdentity: userIdentity,
                    workspaceEncryptionSession: workspaceEncryptionSession,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);
        }

        return currentParentId;
    }

    private static int? ResolveSegmentByIdThenName(
        int workspaceId,
        int segmentId,
        string plainName,
        int? currentParentId,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        // Phase 1: ID lookup — folder still exists in the workspace, possibly moved/renamed.
        // We intentionally do NOT require parent_id == currentParentId: "follow rename/move"
        // semantics means if the user moved /Projects to /Archive/Projects after the file was
        // trashed, the restored file follows the folder to its new home.
        var byId = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT fo_id
                     FROM fo_folders
                     WHERE fo_workspace_id = $workspaceId
                       AND fo_id = $segmentId
                       AND fo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$segmentId", segmentId)
            .Execute();

        if (!byId.IsEmpty)
            return byId.Value;

        // Phase 2: name-fallback. Folder with that ID is gone; look for one with the same name
        // sitting under the current parent. Have to decode sibling names in memory since
        // encrypted name columns aren't directly comparable to a plaintext target.
        var siblings = dbWriteContext
            .Cmd(
                sql: """
                     SELECT fo_id, fo_name
                     FROM fo_folders
                     WHERE fo_workspace_id = $workspaceId
                       AND fo_is_being_deleted = FALSE
                       AND (
                           ($currentParentId IS NULL AND fo_parent_folder_id IS NULL)
                           OR fo_parent_folder_id = $currentParentId
                       )
                     """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    Name = reader.DecodeEncryptableString(1, workspaceEncryptionSession)
                },
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$currentParentId", currentParentId)
            .Execute();

        var match = siblings.FirstOrDefault(s =>
            string.Equals(s.Name, plainName, StringComparison.Ordinal));

        return match?.Id;
    }

    private int CreateFolder(
        int workspaceId,
        int? parentId,
        string plainName,
        IUserIdentity userIdentity,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        // ancestor_folder_ids for the new folder = parent's ancestors + parent; empty for a
        // root-level folder.
        int[] ancestorFolderIds = [];
        if (parentId.HasValue)
        {
            var parentAncestors = dbWriteContext
                .OneRowCmd(
                    sql: """
                         SELECT fo_ancestor_folder_ids
                         FROM fo_folders
                         WHERE fo_id = $parentId AND fo_workspace_id = $workspaceId
                         """,
                    readRowFunc: reader => reader.GetFromJson<int[]>(0),
                    transaction: transaction)
                .WithParameter("$parentId", parentId.Value)
                .WithParameter("$workspaceId", workspaceId)
                .Execute();

            if (!parentAncestors.IsEmpty)
                ancestorFolderIds = [.. parentAncestors.Value, parentId.Value];
        }

        var externalId = FolderExtId.NewId();

        var newId = dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO fo_folders (
                         fo_external_id,
                         fo_workspace_id,
                         fo_parent_folder_id,
                         fo_ancestor_folder_ids,
                         fo_name,
                         fo_is_being_deleted,
                         fo_creator_identity_type,
                         fo_creator_identity,
                         fo_created_at
                     ) VALUES (
                         $externalId,
                         $workspaceId,
                         $parentId,
                         json($ancestorFolderIds),
                         $name,
                         FALSE,
                         $creatorIdentityType,
                         $creatorIdentity,
                         $createdAt
                     )
                     RETURNING fo_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$parentId", parentId)
            .WithJsonParameter("$ancestorFolderIds", ancestorFolderIds)
            .WithEncryptableParameter("$name", workspaceEncryptionSession.ToEncryptableMetadata(plainName))
            .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
            .WithParameter("$creatorIdentity", userIdentity.Identity)
            .WithParameter("$createdAt", clock.UtcNow)
            .ExecuteOrThrow();

        return newId;
    }

    private static string ResolveUniqueName(
        int workspaceId,
        int? destinationFolderId,
        string desiredName,
        string extension,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        // Live siblings in the destination folder, folded into a name+extension set. For
        // collisions we suffix until unique. Reading + decoding all sibling names is acceptable
        // here — restore is low-frequency and folders with thousands of same-level files are rare.
        var taken = dbWriteContext.AggregateRows(
            sql: """
                 SELECT fi_name, fi_extension
                 FROM fi_files
                 WHERE fi_workspace_id = $workspaceId
                   AND fi_deleted_at IS NULL
                   AND fi_parent_file_id IS NULL
                   AND (
                       ($folderId IS NULL AND fi_folder_id IS NULL)
                       OR fi_folder_id = $folderId
                   )
                 """,
            seed: new HashSet<string>(StringComparer.Ordinal),
            aggregateRowFunc: (acc, reader) =>
            {
                var name = reader.DecodeEncryptableString(
                    0, 
                    workspaceEncryptionSession);

                var extension = reader.DecodeEncryptableString(
                    1, 
                    workspaceEncryptionSession);
                
                acc.Add(name + extension);
                return acc;
            },
            transaction: transaction)
        .WithParameter("$workspaceId", workspaceId)
        .WithParameter("$folderId", destinationFolderId)
        .Execute();

        var candidate = desiredName + extension;
        if (!taken.Contains(candidate))
            return desiredName;

        for (var i = 1; i < 1000; i++)
        {
            var suffix = i == 1 ? " (restored)" : $" (restored {i})";
            var candidateName = desiredName + suffix;
            if (!taken.Contains(candidateName + extension))
                return candidateName;
        }

        // Pathological fallback — 1000 same-named restores in the same folder. Append timestamp.
        return $"{desiredName} (restored {DateTime.UtcNow.Ticks})";
    }

    private void UpdateFileBack(
        int fileId,
        int? folderId,
        string finalName,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE fi_files
                     SET fi_deleted_at = NULL,
                         fi_original_folder_path = NULL,
                         fi_folder_id = $folderId,
                         fi_name = $name
                     WHERE fi_id = $fileId
                     RETURNING fi_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$fileId", fileId)
            .WithParameter("$folderId", folderId)
            .WithEncryptableParameter("$name", workspaceEncryptionSession.ToEncryptableMetadata(finalName))
            .ExecuteOrThrow();
    }

    private static void UnTrashChildren(
        int parentFileId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
            .Cmd(
                sql: """
                     UPDATE fi_files
                     SET fi_deleted_at = NULL,
                         fi_original_folder_path = NULL
                     WHERE fi_parent_file_id = $parentId
                       AND fi_deleted_at IS NOT NULL
                     """,
                readRowFunc: reader => 0,
                transaction: transaction)
            .WithParameter("$parentId", parentFileId)
            .Execute();
    }

    private sealed record TrashedFile(
        int Id,
        string Name,
        string Extension,
        List<OriginalFolderPathSegment>? Path);

    private readonly record struct ChosenFolder(int? Id);
}
