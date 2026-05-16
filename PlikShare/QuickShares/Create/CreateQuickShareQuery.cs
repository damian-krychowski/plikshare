using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.QuickShares.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.QuickShares.Create;

public class CreateQuickShareQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<Result> Execute(
        WorkspaceContext workspace,
        UserExtId creatorExternalId,
        string name,
        string? customSlug,
        List<FileExtId> selectedFiles,
        List<FolderExtId> selectedFolders,
        List<FileExtId> excludedFiles,
        List<FolderExtId> excludedFolders,
        QuickShareMode mode,
        bool allowIndividualFileDownload,
        DateTimeOffset? expiresAt,
        string? passwordHashBase64,
        byte[]? passwordSalt,
        int? maxDownloads,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                creatorExternalId: creatorExternalId,
                name: name,
                customSlug: customSlug,
                selectedFiles: selectedFiles,
                selectedFolders: selectedFolders,
                excludedFiles: excludedFiles,
                excludedFolders: excludedFolders,
                mode: mode,
                allowIndividualFileDownload: allowIndividualFileDownload,
                expiresAt: expiresAt,
                passwordHashBase64: passwordHashBase64,
                passwordSalt: passwordSalt,
                maxDownloads: maxDownloads),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        UserExtId creatorExternalId,
        string name,
        string? customSlug,
        List<FileExtId> selectedFiles,
        List<FolderExtId> selectedFolders,
        List<FileExtId> excludedFiles,
        List<FolderExtId> excludedFolders,
        QuickShareMode mode,
        bool allowIndividualFileDownload,
        DateTimeOffset? expiresAt,
        string? passwordHashBase64,
        byte[]? passwordSalt,
        int? maxDownloads)
    {
        string slug;
        if (customSlug is null)
        {
            slug = QuickShareSlug.GenerateAuto();
        }
        else if (!QuickShareSlug.IsValid(customSlug))
        {
            return new Result(ResultCode.SlugInvalid);
        }
        else
        {
            slug = customSlug;
        }

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var creatorId = GetCreatorId(
                creatorExternalId,
                dbWriteContext,
                transaction);

            if (creatorId is null)
            {
                transaction.Rollback();
                return new Result(ResultCode.CreatorNotFound);
            }

            var selectedFileIds = ResolveFiles(
                workspace.Id,
                selectedFiles,
                dbWriteContext,
                transaction);

            if (selectedFileIds.Count != selectedFiles.Count)
            {
                transaction.Rollback();
                return new Result(ResultCode.ItemsNotFound);
            }

            var excludedFileIds = ResolveFiles(
                workspace.Id,
                excludedFiles,
                dbWriteContext,
                transaction);

            if (excludedFileIds.Count != excludedFiles.Count)
            {
                transaction.Rollback();
                return new Result(ResultCode.ItemsNotFound);
            }

            var selectedFolderIds = ResolveFolders(
                workspace.Id,
                selectedFolders,
                dbWriteContext,
                transaction);

            if (selectedFolderIds.Count != selectedFolders.Count)
            {
                transaction.Rollback();
                return new Result(ResultCode.ItemsNotFound);
            }

            var excludedFolderIds = ResolveFolders(
                workspace.Id,
                excludedFolders,
                dbWriteContext,
                transaction);

            if (excludedFolderIds.Count != excludedFolders.Count)
            {
                transaction.Rollback();
                return new Result(ResultCode.ItemsNotFound);
            }

            var externalId = QuickShareExtId.NewId();

            var quickShareId = InsertQuickShare(
                externalId,
                slug,
                workspace.Id,
                creatorId.Value,
                name,
                expiresAt,
                passwordHashBase64,
                passwordSalt,
                maxDownloads,
                mode,
                allowIndividualFileDownload,
                dbWriteContext,
                transaction);

            InsertItems(
                quickShareId,
                selectedFileIds,
                selectedFolderIds,
                excludedFileIds,
                excludedFolderIds,
                dbWriteContext,
                transaction);

            transaction.Commit();

            Log.Information(
                "QuickShare '{ExternalId} ({Id})' was created in Workspace '{WorkspaceExternalId}' (selected: {SelFiles}f/{SelFolders}fo, excluded: {ExFiles}f/{ExFolders}fo).",
                externalId,
                quickShareId,
                workspace.ExternalId,
                selectedFileIds.Count,
                selectedFolderIds.Count,
                excludedFileIds.Count,
                excludedFolderIds.Count);

            return new Result(
                Code: ResultCode.Ok,
                QuickShareExternalId: externalId,
                Slug: slug);
        }
        catch (SqliteException exception)
        {
            transaction.Rollback();

            if (exception.HasUniqueConstraintFailed(tableName: "qsh_quick_shares", columnName: "qsh_slug"))
            {
                return new Result(ResultCode.SlugTaken);
            }

            if (exception.SqliteExtendedErrorCode == SQLiteExtendedErrorCode.ConstraintForeignKey)
            {
                Log.Warning(exception,
                    "Could not create QuickShare in Workspace '{WorkspaceExternalId}' — foreign key violation.",
                    workspace.ExternalId);

                return new Result(ResultCode.ItemsNotFound);
            }

            Log.Error(exception,
                "Something went wrong while creating QuickShare in Workspace '{WorkspaceExternalId}'",
                workspace.ExternalId);
            throw;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Something went wrong while creating QuickShare in Workspace '{WorkspaceExternalId}'",
                workspace.ExternalId);
            throw;
        }
    }

    private int InsertQuickShare(
        QuickShareExtId externalId,
        string slug,
        int workspaceId,
        int creatorId,
        string name,
        DateTimeOffset? expiresAt,
        string? passwordHashBase64,
        byte[]? passwordSalt,
        int? maxDownloads,
        QuickShareMode mode,
        bool allowIndividualFileDownload,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO qsh_quick_shares (
                         qsh_external_id,
                         qsh_workspace_id,
                         qsh_creator_id,
                         qsh_slug,
                         qsh_secret_hash,
                         qsh_name,
                         qsh_created_at,
                         qsh_expires_at,
                         qsh_password_hash,
                         qsh_password_salt,
                         qsh_max_downloads,
                         qsh_downloads_count,
                         qsh_mode,
                         qsh_allow_individual_file_download,
                         qsh_last_accessed_at
                     ) VALUES (
                         $externalId,
                         $workspaceId,
                         $creatorId,
                         $slug,
                         NULL,
                         $name,
                         $createdAt,
                         $expiresAt,
                         $passwordHash,
                         $passwordSalt,
                         $maxDownloads,
                         0,
                         $mode,
                         $allowIndividualFileDownload,
                         NULL
                     )
                     RETURNING qsh_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$creatorId", creatorId)
            .WithParameter("$slug", slug)
            .WithParameter("$name", name)
            .WithParameter("$createdAt", clock.UtcNow)
            .WithParameter("$expiresAt", expiresAt)
            .WithParameter("$passwordHash", passwordHashBase64)
            .WithParameter("$passwordSalt", passwordSalt)
            .WithParameter("$maxDownloads", maxDownloads)
            .WithEnumParameter("$mode", mode)
            .WithParameter("$allowIndividualFileDownload", allowIndividualFileDownload)
            .ExecuteOrThrow();
    }

    private static int? GetCreatorId(
        UserExtId creatorExternalId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: "SELECT u_id FROM u_users WHERE u_external_id = $externalId LIMIT 1",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$externalId", creatorExternalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
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

    private static void InsertItems(
        int quickShareId,
        List<int> selectedFileIds,
        List<int> selectedFolderIds,
        List<int> excludedFileIds,
        List<int> excludedFolderIds,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        InsertFileItems(
            quickShareId,
            selectedFileIds,
            isExcluded: false,
            dbWriteContext,
            transaction);

        InsertFileItems(
            quickShareId,
            excludedFileIds,
            isExcluded: true,
            dbWriteContext,
            transaction);

        InsertFolderItems(
            quickShareId,
            selectedFolderIds,
            isExcluded: false,
            dbWriteContext,
            transaction);

        InsertFolderItems(
            quickShareId,
            excludedFolderIds,
            isExcluded: true,
            dbWriteContext,
            transaction);
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

    public readonly record struct Result(
        ResultCode Code,
        QuickShareExtId QuickShareExternalId = default,
        string? Slug = null);

    public enum ResultCode
    {
        Ok = 0,
        CreatorNotFound,
        ItemsNotFound,
        SlugInvalid,
        SlugTaken
    }
}
