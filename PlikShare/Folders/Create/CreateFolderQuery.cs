using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Folders.Create;

public class CreateFolderQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FolderExtId folderExternalId,
        FolderExtId? parentFolderExternalId,
        string name,
        int? boxFolderId,
        IUserIdentity userIdentity,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                context,
                workspace,
                folderExternalId,
                parentFolderExternalId,
                name,
                boxFolderId,
                userIdentity),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FolderExtId folderExternalId,
        FolderExtId? parentFolderExternalId,
        string name,
        int? boxFolderId,
        IUserIdentity userIdentity)
    {

        if (parentFolderExternalId is null && boxFolderId is null)
        {
            return CreateTopFolder(
                dbWriteContext: dbWriteContext,
                workspace: workspace,
                folderExternalId: folderExternalId,
                name: name,
                userIdentity: userIdentity);
        }

        if (parentFolderExternalId is not null)
        {
            return CreateSubfolder(
                dbWriteContext: dbWriteContext,
                workspace: workspace,
                folderExternalId: folderExternalId,
                parentFolderExternalId: parentFolderExternalId.Value,
                name: name,
                boxFolderId: boxFolderId,
                userIdentity: userIdentity);
        }

        throw new InvalidOperationException(
            "Invalid parameters for creating a folder");
    }

    private ResultCode CreateTopFolder(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FolderExtId folderExternalId,
        string name,
        IUserIdentity userIdentity)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: @"
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
                    ) 
                    VALUES (
                        $externalId,
                        $workspaceId,
                        NULL,
                        json($ancestorFolderIds),
                        $name,
                        FALSE,
                        $creatorIdentityType,
                        $creatorIdentity,
                        $createdAt    
                    ) 
                    RETURNING
                        fo_id
                ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", folderExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$ancestorFolderIds", "[]")
                .WithParameter("$name", name)
                .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
                .WithParameter("$creatorIdentity", userIdentity.Identity)
                .WithParameter("$createdAt", clock.UtcNow)
                .Execute();

            if (result.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Could not create Top Folder in Workspace '{workspace.Id}' because something went wrong");
            }

            transaction.Commit();

            Log.Information("Top Folder '{FolderExternalId} ({FolderId})' was created",
                folderExternalId,
                result.Value);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while creating top folder");
            throw;
        }
    }

    private ResultCode CreateSubfolder(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FolderExtId folderExternalId,
        FolderExtId parentFolderExternalId,
        string name,
        int? boxFolderId,
        IUserIdentity userIdentity)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var parentFolderResult = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        SELECT 
                            fo_id, 
                            fo_ancestor_folder_ids
                        FROM 
                            fo_folders
                        WHERE 
                            fo_external_id = $parentExternalId
                            AND fo_workspace_id = $workspaceId
                            AND fo_is_being_deleted = FALSE
                            AND (
                                $boxFolderId IS NULL 
                                OR $boxFolderId = fo_id 
                                OR $boxFolderId IN (
                                    SELECT value FROM json_each(fo_ancestor_folder_ids) 
                                ) 
                            )
                        LIMIT 1
                    ",
                    readRowFunc: reader => new ParentFolder(
                        Id: reader.GetInt32(0),
                        AncestorFolderIds: reader.GetFromJson<int[]>(1)),
                    transaction: transaction)
                .WithParameter("$parentExternalId", parentFolderExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$boxFolderId", boxFolderId)
                .Execute();

            if (parentFolderResult.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning("Could not create SubFolder because ParentFolder '{ParentFolderExternalId}' was not found.",
                    parentFolderExternalId);

                return ResultCode.ParentFolderNotFound;
            }

            int[] ancestorFolderIds = [.. parentFolderResult.Value.AncestorFolderIds, parentFolderResult.Value.Id];

            var result = dbWriteContext
                .OneRowCmd(
                    sql: @"
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
                        ) 
                        VALUES (
                            $externalId,
                            $workspaceId,
                            $parentFolderId,
                            json($ancestorFolderIds),
                            $name,
                            FALSE,
                            $creatorIdentityType,
                            $creatorIdentity,
                            $createdAt
                        ) 
                        RETURNING
                            fo_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", folderExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$parentFolderId", parentFolderResult.Value.Id)
                .WithJsonParameter("$ancestorFolderIds", ancestorFolderIds)
                .WithParameter("$name", name)
                .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
                .WithParameter("$creatorIdentity", userIdentity.Identity)
                .WithParameter("$createdAt", clock.UtcNow)
                .Execute();

            if (result.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Could not create SubFolder in Workspace '{workspace.Id}' and ParentFolder '{parentFolderExternalId}' because something went wrong");
            }

            transaction.Commit();

            Log.Information(
                "SubFolder '{FolderExternalId} ({FolderId})' was created in ParentFolder '{ParentFolderExternalId}'",
                folderExternalId,
                result.Value,
                parentFolderExternalId);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while creating top folder");
            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        ParentFolderNotFound
    }

    private readonly record struct ParentFolder(
        int Id,
        int[] AncestorFolderIds);
}