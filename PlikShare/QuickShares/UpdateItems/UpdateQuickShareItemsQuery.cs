using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.UpdateItems;

public class UpdateQuickShareItemsQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        QuickShareContext quickShare,
        List<FileExtId> selectedFiles,
        List<FolderExtId> selectedFolders,
        List<FileExtId> excludedFiles,
        List<FolderExtId> excludedFolders,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                quickShare: quickShare,
                selectedFiles: selectedFiles,
                selectedFolders: selectedFolders,
                excludedFiles: excludedFiles,
                excludedFolders: excludedFolders),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare,
        List<FileExtId> selectedFiles,
        List<FolderExtId> selectedFolders,
        List<FileExtId> excludedFiles,
        List<FolderExtId> excludedFolders)
    {
        if (selectedFiles.Count == 0 && selectedFolders.Count == 0)
            return ResultCode.NoItems;

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var selectedFileIds = ResolveFiles(
                quickShare.Workspace.Id,
                selectedFiles,
                dbWriteContext,
                transaction);

            if (selectedFileIds.Count != selectedFiles.Count)
            {
                transaction.Rollback();
                return ResultCode.ItemsNotFound;
            }

            var excludedFileIds = ResolveFiles(
                quickShare.Workspace.Id,
                excludedFiles,
                dbWriteContext,
                transaction);

            if (excludedFileIds.Count != excludedFiles.Count)
            {
                transaction.Rollback();
                return ResultCode.ItemsNotFound;
            }

            var selectedFolderIds = ResolveFolders(
                quickShare.Workspace.Id,
                selectedFolders,
                dbWriteContext,
                transaction);

            if (selectedFolderIds.Count != selectedFolders.Count)
            {
                transaction.Rollback();
                return ResultCode.ItemsNotFound;
            }

            var excludedFolderIds = ResolveFolders(
                quickShare.Workspace.Id,
                excludedFolders,
                dbWriteContext,
                transaction);

            if (excludedFolderIds.Count != excludedFolders.Count)
            {
                transaction.Rollback();
                return ResultCode.ItemsNotFound;
            }

            DeleteAllItems(
                quickShare.Id,
                dbWriteContext,
                transaction);

            InsertFileItems(
                quickShare.Id,
                selectedFileIds,
                isExcluded: false,
                dbWriteContext,
                transaction);

            InsertFileItems(
                quickShare.Id,
                excludedFileIds,
                isExcluded: true,
                dbWriteContext,
                transaction);

            InsertFolderItems(
                quickShare.Id,
                selectedFolderIds,
                isExcluded: false,
                dbWriteContext,
                transaction);

            InsertFolderItems(
                quickShare.Id,
                excludedFolderIds,
                isExcluded: true,
                dbWriteContext,
                transaction);

            transaction.Commit();

            Log.Information(
                "QuickShare '{ExternalId} ({Id})' items updated (selected: {SelFiles}f/{SelFolders}fo, excluded: {ExFiles}f/{ExFolders}fo).",
                quickShare.ExternalId,
                quickShare.Id,
                selectedFileIds.Count,
                selectedFolderIds.Count,
                excludedFileIds.Count,
                excludedFolderIds.Count);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Something went wrong while updating items of QuickShare '{ExternalId}'",
                quickShare.ExternalId);
            throw;
        }
    }

    private static void DeleteAllItems(
        int quickShareId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
            .Cmd(
                sql: """
                     DELETE FROM qshi_quick_share_items
                     WHERE qshi_quick_share_id = $quickShareId
                     RETURNING qshi_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$quickShareId", quickShareId)
            .Execute();
    }

    private static List<int> ResolveFiles(
        int workspaceId,
        List<FileExtId> fileExternalIds,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileExternalIds.Count == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: """
                     SELECT fi_id
                     FROM fi_files
                     WHERE
                         fi_workspace_id = $workspaceId
                         AND fi_is_upload_completed = TRUE
                         AND fi_external_id IN (SELECT value FROM json_each($externalIds))
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$externalIds", fileExternalIds)
            .Execute();
    }

    private static List<int> ResolveFolders(
        int workspaceId,
        List<FolderExtId> folderExternalIds,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderExternalIds.Count == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: """
                     SELECT fo_id
                     FROM fo_folders
                     WHERE
                         fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                         AND fo_external_id IN (SELECT value FROM json_each($externalIds))
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$externalIds", folderExternalIds)
            .Execute();
    }

    private static void InsertFileItems(
        int quickShareId,
        List<int> fileIds,
        bool isExcluded,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileIds.Count == 0)
            return;

        dbWriteContext
            .Cmd(
                sql: """
                     INSERT INTO qshi_quick_share_items (qshi_quick_share_id, qshi_file_id, qshi_folder_id, qshi_is_excluded)
                     SELECT $quickShareId, value, NULL, $isExcluded
                     FROM json_each($fileIds)
                     RETURNING qshi_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$quickShareId", quickShareId)
            .WithParameter("$isExcluded", isExcluded)
            .WithJsonParameter("$fileIds", fileIds)
            .Execute();
    }

    private static void InsertFolderItems(
        int quickShareId,
        List<int> folderIds,
        bool isExcluded,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderIds.Count == 0)
            return;

        dbWriteContext
            .Cmd(
                sql: """
                     INSERT INTO qshi_quick_share_items (qshi_quick_share_id, qshi_file_id, qshi_folder_id, qshi_is_excluded)
                     SELECT $quickShareId, NULL, value, $isExcluded
                     FROM json_each($folderIds)
                     RETURNING qshi_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$quickShareId", quickShareId)
            .WithParameter("$isExcluded", isExcluded)
            .WithJsonParameter("$folderIds", folderIds)
            .Execute();
    }

    public enum ResultCode
    {
        Ok = 0,
        NoItems,
        ItemsNotFound
    }
}
