using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Folders.Id;
using PlikShare.Folders.UpdatePositions.Contracts;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Folders.UpdatePositions;

public class UpdatePositionsQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FolderExtId? parentFolderExternalId,
        int? boxFolderId,
        List<UpdatePositionItemDto> folders,
        List<UpdatePositionItemDto> files,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                parentFolderExternalId: parentFolderExternalId,
                boxFolderId: boxFolderId,
                folders: folders,
                files: files),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        FolderExtId? parentFolderExternalId,
        int? boxFolderId,
        List<UpdatePositionItemDto> folders,
        List<UpdatePositionItemDto> files)
    {
        if (folders.Count == 0 && files.Count == 0)
            return ResultCode.Ok;

        if (parentFolderExternalId is null && boxFolderId is not null)
            return ResultCode.ParentFolderNotFound;

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var (parentFolderId, errorCode) = TryGetParentFolderId(
                dbWriteContext,
                parentFolderExternalId,
                workspace.Id,
                boxFolderId,
                transaction);

            if (errorCode is not null)
            {
                transaction.Rollback();
                return errorCode.Value;
            }

            var folderItems = ReadFolderItems(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspaceId: workspace.Id,
                parentFolderId: parentFolderId);
            
            if (!ApplyUpdates(folderItems, folders))
            {
                transaction.Rollback();
                return ResultCode.SomeFoldersNotFound;
            }

            var fileItems = ReadFileItems(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspaceId: workspace.Id,
                parentFolderId: parentFolderId);
            
            if (!ApplyUpdates(fileItems, files))
            {
                transaction.Rollback();
                return ResultCode.SomeFilesNotFound;
            }

            if (HasCollision(folderItems))
                RebalanceInMemory(folderItems, tieBreakDescending: false);

            if (HasCollision(fileItems))
                RebalanceInMemory(fileItems, tieBreakDescending: true);

            FlushFolderChanges(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                items: folderItems);

            FlushFileChanges(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                items: fileItems);

            transaction.Commit();

            Log.Information(
                "Positions updated in Workspace#{WorkspaceId}, ParentFolder '{ParentFolder}'. " +
                "Folders submitted: {FoldersCount}, Files submitted: {FilesCount}",
                workspace.Id,
                parentFolderExternalId?.Value ?? "<top>",
                folders.Count,
                files.Count);

            return ResultCode.Ok;
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            Log.Error(ex,
                "Error updating positions in Workspace#{WorkspaceId}, ParentFolder '{ParentFolder}'",
                workspace.Id,
                parentFolderExternalId?.Value ?? "<top>");

            throw;
        }
    }

    private static (int? ParentFolderId, ResultCode? Code) TryGetParentFolderId(
        SqliteWriteContext dbWriteContext,
        FolderExtId? parentFolderExternalId,
        int workspaceId,
        int? boxFolderId,
        SqliteTransaction transaction)
    {
        if (parentFolderExternalId is null)
            return (null, null);

        var parentResult = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT fo_id
                     FROM fo_folders
                     WHERE fo_external_id = $parentExternalId
                       AND fo_workspace_id = $workspaceId
                       AND fo_is_being_deleted = FALSE
                       AND (
                           $boxFolderId IS NULL
                           OR $boxFolderId = fo_id
                           OR $boxFolderId IN (
                               SELECT value FROM json_each(fo_ancestor_folder_ids)
                           )
                       )
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$parentExternalId", parentFolderExternalId.Value.Value)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();

        if (parentResult.IsEmpty)
            return (null, ResultCode.ParentFolderNotFound);

        return (parentResult.Value, null);
    }

    private static List<ItemState> ReadFolderItems(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        int workspaceId,
        int? parentFolderId)
    {
        long maxPosition = 0;

        return dbWriteContext
            .Cmd(
                sql: """
                     SELECT fo_id, fo_external_id, fo_position
                     FROM fo_folders
                     WHERE fo_workspace_id = $workspaceId
                       AND fo_is_being_deleted = FALSE
                       AND (
                           ($parentFolderId IS NULL AND fo_parent_folder_id IS NULL)
                           OR fo_parent_folder_id = $parentFolderId
                       )
                     ORDER BY
                         (fo_position IS NULL),
                         fo_position,
                         fo_id
                     """,
                readRowFunc: reader =>
                {
                    var storedPosition = reader.GetInt64OrNull(2);

                    (var effectivePosition, maxPosition) = List.ItemPosition.Calculate(
                        storedPosition: storedPosition,
                        maxPosition: maxPosition);

                    return new ItemState
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetString(1),
                        StoredPosition = storedPosition,
                        NewPosition = effectivePosition
                    };
                },
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$parentFolderId", parentFolderId)
            .Execute();
    }

    private static List<ItemState> ReadFileItems(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        int workspaceId,
        int? parentFolderId)
    {
        long maxPosition = 0;

        return dbWriteContext
            .Cmd(
                sql: """
                     SELECT fi_id, fi_external_id, fi_position
                     FROM fi_files
                     WHERE fi_workspace_id = $workspaceId
                       AND fi_parent_file_id IS NULL
                       AND (
                           ($parentFolderId IS NULL AND fi_folder_id IS NULL)
                           OR fi_folder_id = $parentFolderId
                       )
                     ORDER BY
                         (fi_position IS NULL),
                         fi_position,
                         fi_id DESC
                     """,
                readRowFunc: reader =>
                {
                    var storedPosition = reader.GetInt64OrNull(2);
                    
                    (var effectivePosition, maxPosition) = List.ItemPosition.Calculate(
                        storedPosition: storedPosition,
                        maxPosition: maxPosition);

                    return new ItemState
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetString(1),
                        StoredPosition = storedPosition,
                        NewPosition = effectivePosition
                    };
                },
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$parentFolderId", parentFolderId)
            .Execute();
    }

    private static bool ApplyUpdates(
        List<ItemState> items, 
        List<UpdatePositionItemDto> updates)
    {
        if (updates.Count == 0)
            return true;

        var byExternalId = items.ToDictionary(i => i.ExternalId);

        foreach (var update in updates)
        {
            if (!byExternalId.TryGetValue(update.ExternalId, out var item))
                return false;

            item.NewPosition = update.Position;
        }

        return true;
    }

    private static bool HasCollision(List<ItemState> items)
    {
        var seen = new HashSet<long>();

        foreach (var item in items)
        {
            if (item.NewPosition is null)
                continue;

            if (!seen.Add(item.NewPosition.Value))
                return true;
        }

        return false;
    }

    private static void RebalanceInMemory(List<ItemState> items, bool tieBreakDescending)
    {
        var sorted = items
            .OrderBy(i => i.NewPosition.HasValue ? 0 : 1)
            .ThenBy(i => i.NewPosition ?? 0L)
            .ThenBy(i => tieBreakDescending ? -i.Id : i.Id)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].NewPosition = (i + 1L) * List.ItemPosition.Step;
        }
    }

    private static void FlushFolderChanges(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        List<ItemState> items)
    {
        var changes = items
            .Where(i => i.NewPosition != i.StoredPosition)
            .Select(i => new
            {
                i.ExternalId, 
                Position = i.NewPosition
            })
            .ToList();

        if (changes.Count == 0)
            return;

        dbWriteContext
            .Cmd(
                sql: """
                     UPDATE fo_folders
                     SET fo_position = json_extract(j.value, '$.position')
                     FROM json_each($items) AS j
                     WHERE fo_folders.fo_external_id = json_extract(j.value, '$.externalId')
                     RETURNING fo_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithJsonParameter("$items", changes)
            .Execute();
    }

    private static void FlushFileChanges(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        List<ItemState> items)
    {
        var changes = items
            .Where(i => i.NewPosition != i.StoredPosition)
            .Select(i => new
            {
                i.ExternalId, 
                Position = i.NewPosition
            })
            .ToList();

        if (changes.Count == 0)
            return;

        dbWriteContext
            .Cmd(
                sql: """
                     UPDATE fi_files
                     SET fi_position = json_extract(j.value, '$.position')
                     FROM json_each($items) AS j
                     WHERE fi_files.fi_external_id = json_extract(j.value, '$.externalId')
                     RETURNING fi_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithJsonParameter("$items", changes)
            .Execute();
    }

    private sealed class ItemState
    {
        public required int Id { get; init; }
        public required string ExternalId { get; init; }
        public required long? StoredPosition { get; init; }
        public long? NewPosition { get; set; }
    }

    public enum ResultCode
    {
        Ok = 0,
        ParentFolderNotFound,
        SomeFoldersNotFound,
        SomeFilesNotFound
    }
}
