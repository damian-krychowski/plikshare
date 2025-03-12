using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Boxes.Create;

public class CreateBoxQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        WorkspaceContext workspace,
        string name,
        FolderExtId folderExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                name: name,
                folderExternalId: folderExternalId),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        string name,
        FolderExtId folderExternalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var getFolderResult = GetFolder(
                workspace,
                folderExternalId,
                dbWriteContext,
                transaction);

            if (getFolderResult.IsEmpty)
            {
                transaction.Rollback();
                
                Log.Warning("Could not create Box because Folder '{FolderExternalId}' was not found.",
                    folderExternalId);

                return new Result(
                    Code: ResultCode.FolderWasNotFound);
            }
            
            var box = InsertBoxResultOrThrow(
                workspace,
                name,
                getFolderResult.Value.Id,
                dbWriteContext,
                transaction);
            
            transaction.Commit();
            
            Log.Information("Box '{BoxExternalId} ({BoxId})' was created with Folder '{FolderExternalId}' in Workspace '{WorkspaceExternalId}'.",
                box.ExternalId,
                box.Id,
                folderExternalId,
                workspace.ExternalId);

            return new Result(
                Code: ResultCode.Ok,
                BoxExternalId: box.ExternalId);
        }
        catch (SqliteException exception)
        {
            transaction.Rollback();
            
            if (exception.SqliteExtendedErrorCode == SQLiteExtendedErrorCode.ConstraintForeignKey)
            {
                Log.Warning(exception, "Could not create Box because Folder '{FolderExternalId}' was not found.",
                    folderExternalId);
                    
                return new Result(
                    Code: ResultCode.FolderWasNotFound);
            }

            Log.Error(exception, "Something went wrong while creating Box with Folder '{FolderExternalId}'",
                folderExternalId);
            
            throw;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(e, "Something went wrong while creating Box with Folder '{FolderExternalId}'",
                folderExternalId);
            
            throw;
        }
    }

    private static Box InsertBoxResultOrThrow(
        WorkspaceContext workspace,
        string name,
        int folderId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var externalId = BoxExtId.NewId();
        
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO bo_boxes (
                         bo_external_id,
                         bo_workspace_id,
                         bo_folder_id,
                         bo_is_enabled,
                         bo_name,
                         bo_is_being_deleted,
                         bo_header_is_enabled,
                         bo_header_json,
                         bo_header_html,
                         bo_footer_is_enabled,
                         bo_footer_json,
                         bo_footer_html              
                     ) VALUES (
                         $externalId,
                         $workspaceId,
                         $folderId,
                         TRUE,
                         $name,
                         FALSE,
                         FALSE,
                         NULL,
                         NULL,
                         FALSE,
                         NULL,
                         NULL
                     )
                     RETURNING 
                         bo_id
                     """,
                readRowFunc: reader => new Box(
                    Id: reader.GetInt32(0),
                    ExternalId: externalId),
                transaction: transaction)
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$folderId", folderId)
            .WithParameter("$name", name)
            .ExecuteOrThrow();
    }

    private static SQLiteOneRowCommandResult<Folder> GetFolder(
        WorkspaceContext workspace, 
        FolderExtId folderExternalId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT
                         fo_id
                     FROM fo_folders
                     WHERE
                         fo_external_id = $folderExternalId
                         AND fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => new Folder(
                    Id: reader.GetInt32(0)),
                transaction: transaction)
            .WithParameter("$folderExternalId", folderExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private readonly record struct Folder(
        int Id);
    
    public readonly record struct Result(
        ResultCode Code,
        BoxExtId BoxExternalId = default);

    private readonly record struct Box(
        int Id,
        BoxExtId ExternalId);
    
    public enum ResultCode
    {
        Ok = 0,
        FolderWasNotFound
    }
}