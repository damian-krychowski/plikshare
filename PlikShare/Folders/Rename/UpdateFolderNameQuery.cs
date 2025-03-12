using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Folders.Rename;

public class UpdateFolderNameQuery(
    IClock clock,
    DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FolderExtId folderExternalId,
        string name,
        int? boxFolderId,
        IUserIdentity userIdentity,
        bool isOperationAllowedByBoxPermissions,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                folderExternalId: folderExternalId,
                name: name,
                boxFolderId: boxFolderId,
                userIdentity: userIdentity,
                isOperationAllowedByBoxPermissions: isOperationAllowedByBoxPermissions),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FolderExtId folderExternalId,
        string name,
        int? boxFolderId,
        IUserIdentity userIdentity,
        bool isOperationAllowedByBoxPermissions)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var getFolderResult = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE fo_folders
                         SET fo_name = $name
                         WHERE
                             fo_external_id = $folderExternalId
                             AND fo_workspace_id = $workspaceId
                             AND (
                                 $boxFolderId IS NULL 
                                 OR (
                                     $boxFolderId IN (
                                         SELECT value FROM json_each(fo_ancestor_folder_ids) 
                                     )
                                     AND (
                                         $isOperationAllowedByBoxPermissions = TRUE 
                                         OR (
                                             fo_creator_identity = $creatorIdentity
                                             AND fo_creator_identity_type = $creatorIdentityType
                                             AND fo_created_at IS NOT NULL
                                             AND fo_created_at >= $allowedRenameDeadline
                                         )
                                     )
                                 )
                             )
                         RETURNING
                             fo_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$name", name)
                .WithParameter("$folderExternalId", folderExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$boxFolderId", boxFolderId)
                .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
                .WithParameter("$creatorIdentity", userIdentity.Identity)
                .WithParameter("$isOperationAllowedByBoxPermissions", isOperationAllowedByBoxPermissions)
                .WithParameter("$allowedRenameDeadline", clock.UtcNow.AddMinutes(-5)) //there are 5 minutes to rename folder in case rename permission is not granted
                .Execute();

            if (getFolderResult.IsEmpty)
            {
                transaction.Rollback();
                
                Log.Warning(
                    "Could not update Folder '{FolderExternalId}' name to '{NewFolderName}' because Folder was not found.",
                    folderExternalId,
                    name);

                return ResultCode.FolderNotFound;
            }
            
            transaction.Commit();
            
            Log.Information("Folder '{FolderExternalId} ({FolderId})' name was updated to '{Name}'",
                folderExternalId,
                getFolderResult.Value,
                name);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while updating Folder '{FolderExternalId}' name to '{NewFolderName}'",
                folderExternalId,
                name);
            
            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        FolderNotFound
    }
}