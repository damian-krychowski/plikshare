using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.Rename;

public class UpdateFileNameQuery(DbWriteQueue dbWriteQueue)
{
    public  Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        string name,
        int? boxFolderId,
        IUserIdentity userIdentity,
        bool isRenameAllowedByBoxPermissions,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                fileExternalId: fileExternalId,
                name: name,
                boxFolderId: boxFolderId,
                userIdentity: userIdentity,
                isRenameAllowedByBoxPermissions: isRenameAllowedByBoxPermissions),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        string name,
        int? boxFolderId,
        IUserIdentity userIdentity,
        bool isRenameAllowedByBoxPermissions)
    {
        var result = boxFolderId is null
            ? UpdateFileNameInWorkspace(
                workspace,
                fileExternalId,
                name,
                dbWriteContext)
            : UpdateFileNameInBox(
                workspace,
                fileExternalId,
                name,
                boxFolderId,
                userIdentity,
                isRenameAllowedByBoxPermissions,
                dbWriteContext);

        if (result.IsEmpty)
        {
            Log.Warning("Could not update File '{FileExternalId}' name to '{Name}' because File was not found.",
                fileExternalId,
                name);

            return ResultCode.FileNotFound;
        }

        Log.Information("File '{FileExternalId} ({FileId})' name was updated to '{Name}'",
            fileExternalId,
            result.Value,
            name);

        return ResultCode.Ok;
    }

    private static SQLiteOneRowCommandResult<int> UpdateFileNameInWorkspace(
        WorkspaceContext workspace, 
        FileExtId fileExternalId, 
        string name,
        DbWriteQueue.Context dbWriteContext)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE fi_files
                    SET fi_name = $name
                    WHERE
                        fi_external_id = $fileExternalId
                        AND fi_workspace_id = $workspaceId        
                    RETURNING
                        fi_id
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$name", name)
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private static SQLiteOneRowCommandResult<int> UpdateFileNameInBox(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        string name,
        int? boxFolderId,
        IUserIdentity userIdentity,
        bool isRenameAllowedByBoxPermissions,
        DbWriteQueue.Context dbWriteContext)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE fi_files
                    SET fi_name = $name
                    WHERE
                        fi_external_id = $fileExternalId
                        AND fi_workspace_id = $workspaceId
                        AND (
                            $isRenameAllowedByBoxPermissions = TRUE 
                            OR (
                                fi_uploader_identity = $uploaderIdentity
                                AND fi_uploader_identity_type = $uploaderIdentityType
                            )
                        )
                        AND EXISTS (
                            SELECT 1
                            FROM fo_folders
                            WHERE 
                                fo_id = fi_folder_id
                                AND fo_workspace_id = $workspaceId
                                AND fo_is_being_deleted = FALSE
                                AND (
                                    $boxFolderId = fo_id
                                    OR $boxFolderId IN (
                                        SELECT value FROM json_each(fo_ancestor_folder_ids) 
                                    )
                                )
                        )         
                    RETURNING
                        fi_id
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$name", name)
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithParameter("$isRenameAllowedByBoxPermissions", isRenameAllowedByBoxPermissions)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .Execute();
    }

    public enum ResultCode
    {
        Ok = 0,
        FileNotFound
    }
}