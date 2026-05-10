using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;
using PlikShare.Folders.List;
using PlikShare.Folders.UpdatePositions.Contracts;

namespace PlikShare.Folders.UpdatePositions;

public static class ApplyItemPositionsOperation
{
    public enum ResultCode
    {
        Ok,
        SomeFoldersNotFound,
        SomeFilesNotFound
    }

    public static ResultCode Execute(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        int workspaceId,
        int? parentFolderId,
        List<UpdatePositionItemDto> folders,
        List<UpdatePositionItemDto> files)
    {
        var folderItems = ReadFolderItems(
            dbWriteContext: dbWriteContext,
            transaction: transaction,
            workspaceId: workspaceId,
            parentFolderId: parentFolderId);

        if (!ApplyUpdates(folderItems, folders))
            return ResultCode.SomeFoldersNotFound;

        var fileItems = ReadFileItems(
            dbWriteContext: dbWriteContext,
            transaction: transaction,
            workspaceId: workspaceId,
            parentFolderId: parentFolderId);

        if (!ApplyUpdates(fileItems, files))
            return ResultCode.SomeFilesNotFound;

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

        return ResultCode.Ok;
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

                    (var effectivePosition, maxPosition) = ItemPosition.Calculate(
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

                    (var effectivePosition, maxPosition) = ItemPosition.Calculate(
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
            sorted[i].NewPosition = (i + 1L) * ItemPosition.Step;
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
}
