using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Folders.Id;
using Serilog;

namespace PlikShare.Boxes.UpdateFolder;

public class UpdateBoxFolderQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxContext box,
        FolderExtId folderExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                folderExternalId: folderExternalId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        FolderExtId folderExternalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        
        try
        {
            var getFolderResult = GetFolder(
                box, 
                folderExternalId, 
                dbWriteContext, 
                transaction);

            if (getFolderResult.IsEmpty)
            {
                transaction.Rollback();
                
                Log.Warning("Could not update Box '{BoxExternalId}' Folder to '{FolderExternalId'} because Folder was not found.",
                    box.ExternalId,
                    folderExternalId);

                return ResultCode.FolderNotFound;
            }
            
            var updateBoxResult = UpdateBox(
                getFolderResult.Value,
                box,
                dbWriteContext, 
                transaction);
            
            if (updateBoxResult.IsEmpty)
            {
                transaction.Rollback();
                
                Log.Warning("Could not update Box '{BoxExternalId}' Folder to '{FolderExternalId'} because Box was not found.",
                    box.ExternalId,
                    folderExternalId);

                return ResultCode.BoxNotFound;
            }

            transaction.Commit();
            
            Log.Information("Box '{BoxExternalId}' Folder was updated to '{FolderExternalId'}",
                box.ExternalId,
                folderExternalId);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(e,
                "Something went wrong while updating Box '{BoxExternalId}' Folder to '{FolderExternalId'}.",
                box.ExternalId,
                folderExternalId);
            
            throw;
        }
    }

    private static SQLiteOneRowCommandResult<int> UpdateBox(
        int folderId,
        BoxContext box,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql:"""
                    UPDATE bo_boxes
                    SET bo_folder_id = $folderId
                    WHERE bo_id = $boxId                        
                    RETURNING bo_id
                    """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$folderId", folderId)
            .WithParameter("$boxId", box.Id)
            .Execute();
    }

    private static SQLiteOneRowCommandResult<int> GetFolder(
        BoxContext box,
        FolderExtId folderExternalId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT fo_id
                     FROM fo_folders
                     WHERE 
                         fo_external_id = $folderExternalId
                         AND fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$folderExternalId", folderExternalId.Value)
            .WithParameter("$workspaceId", box.Workspace.Id)
            .Execute();
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound,
        FolderNotFound
    }
}