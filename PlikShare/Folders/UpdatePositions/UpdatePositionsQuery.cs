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

            var operationResult = ApplyItemPositionsOperation.Execute(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspaceId: workspace.Id,
                parentFolderId: parentFolderId,
                folders: folders,
                files: files);

            if (operationResult != ApplyItemPositionsOperation.ResultCode.Ok)
            {
                transaction.Rollback();
                return Map(operationResult);
            }

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

    private static ResultCode Map(ApplyItemPositionsOperation.ResultCode code) => code switch
    {
        ApplyItemPositionsOperation.ResultCode.Ok => ResultCode.Ok,
        ApplyItemPositionsOperation.ResultCode.SomeFoldersNotFound => ResultCode.SomeFoldersNotFound,
        ApplyItemPositionsOperation.ResultCode.SomeFilesNotFound => ResultCode.SomeFilesNotFound,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
    };

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

    public enum ResultCode
    {
        Ok = 0,
        ParentFolderNotFound,
        SomeFoldersNotFound,
        SomeFilesNotFound
    }
}
