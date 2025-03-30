using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Delete;
using PlikShare.Integrations.Aws.Textract.Jobs.Delete;
using PlikShare.Uploads.Delete;
using PlikShare.Users.Entities;
using PlikShare.Workspaces.DeleteBucket;
using PlikShare.Workspaces.Id;
using Serilog;
using Serilog.Events;

namespace PlikShare.Workspaces.Delete.QueueJob;

public class DeleteWorkspaceWithDependenciesQuery(
    IClock clock,
    IQueue queue,
    DeleteFilesSubQuery deleteFilesSubQuery,
    DeleteFileUploadsSubQuery deleteFileUploadsSubQuery,
    DeleteTextractJobsSubQuery deleteTextractJobsSubQuery)
{
    public Result Execute(
        int workspaceId,
        DateTimeOffset deletedAt,
        Guid correlationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var workspace = dbWriteContext
            .OneRowCmd(
                sql: """
                    SELECT
                        w_name,
                        w_external_id,
                        w_bucket_name,
                        w_storage_id
                    FROM 
                        w_workspaces
                    WHERE
                        w_id = $workspaceId
                    LIMIT 1
                    """,
                readRowFunc: reader => new
                {
                    Name = reader.GetString(0),
                    ExternalId = reader.GetExtId<WorkspaceExtId>(1),
                    BucketName = reader.GetString(2),
                    StorageId = reader.GetInt32(3)
                },
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        if (workspace.IsEmpty)
        {
            Log.Warning("Workspace#{WorkspaceId} was not found, workspace delete operation will be cancelled.",
                workspaceId);

            return new Result(Code: ResultCode.WorkspaceNotFound);
        }

        dbWriteContext.DeferForeignKeys(
            transaction);

        var deleteWorkspaceSagaId = queue.InsertSaga(
            correlationId: correlationId,
            onCompletedJobType: DeleteBucketQueueJobType.Value,
            onCompletedJobDefinition: new DeleteBucketQueueJobDefinition
            {
                BucketName = workspace.Value.BucketName,
                StorageId = workspace.Value.StorageId
            },
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        var deletedBoxLinks = DeleteBoxLinks(
            workspaceId,
            dbWriteContext,
            transaction);
        
        var deletedBoxMemberships = DeleteBoxMemberships(
            workspaceId,
            dbWriteContext,
            transaction);

        var jobsToEnqueue = new List<BulkQueueJobEntity>();

        foreach (var deletedBoxMembership in deletedBoxMemberships)
        {
            var job = queue.CreateBulkEntity(
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<BoxMembershipRevokedEmailDefinition>
                {
                    Email = deletedBoxMembership.MemberEmail.Value,
                    Definition = new BoxMembershipRevokedEmailDefinition
                    {
                        BoxName = deletedBoxMembership.BoxName
                    },
                    Template = EmailTemplate.BoxMembershipRevoked,
                },
                sagaId: null);

            jobsToEnqueue.Add(job);
        }

        var deletedBoxes = DeleteBoxes(
            workspaceId,
            dbWriteContext,
            transaction);

        var filesToDelete = GetFilesToDelete(
            workspaceId,
            dbWriteContext,
            transaction);

        var (deletedFiles, filesJobs) = deleteFilesSubQuery.Execute(
            workspaceId: workspaceId,
            fileIds: filesToDelete,
            sagaId: deleteWorkspaceSagaId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        jobsToEnqueue.AddRange(filesJobs);

        var deletedFileIds = deletedFiles
            .Select(x => x.Id)
            .ToArray();
        
        var fileUploadsToDelete = GetFileUploadsToDelete(
            workspaceId,
            dbWriteContext,
            transaction);

        var (deletedFileUploads, deletedFileUploadParts, fileUploadJobs) = deleteFileUploadsSubQuery.Execute(
            fileUploadIds: fileUploadsToDelete,
            sagaId: deleteWorkspaceSagaId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        jobsToEnqueue.AddRange(fileUploadJobs);

        var deletedCopyFileQueueJobs = DeleteCopyFileQueueJobs(
            workspaceId,
            dbWriteContext,
            transaction);

        var copyFileUploadsToDelete = deletedCopyFileQueueJobs
            .Where(df => df.DestinationWorkspaceId != workspaceId)
            .Select(df => df.FileUploadId)
            .ToList();

        var (deletedCopyFileUploads, deletedCopyFileUploadParts, copyFileUploadJobs) = deleteFileUploadsSubQuery.Execute(
            fileUploadIds: copyFileUploadsToDelete,
            sagaId: deleteWorkspaceSagaId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        jobsToEnqueue.AddRange(copyFileUploadJobs);
        
        var deletedTextractJobs = deleteTextractJobsSubQuery.Execute(
            workspaceId: workspaceId,
            deletedFileIds: deletedFileIds,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        var deletedFolders = DeleteFolders(
            workspaceId,
            dbWriteContext,
            transaction);

        var deletedWorkspaceMemberships = DeleteWorkspaceMemberships(
            workspaceId,
            dbWriteContext,
            transaction);

        foreach (var deletedWorkspaceMembership in deletedWorkspaceMemberships)
        {
            var job = queue.CreateBulkEntity(
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<WorkspaceMembershipRevokedEmailDefinition>
                {
                    Email = deletedWorkspaceMembership.MemberEmail.Value,
                    Definition = new WorkspaceMembershipRevokedEmailDefinition
                    {
                        WorkspaceName = workspace.Value.Name
                    },
                    Template = EmailTemplate.WorkspaceMembershipRevoked
                },
                sagaId: null);

            jobsToEnqueue.Add(job);
        }

        var queueJobs = queue.EnqueueBulk(
            correlationId: correlationId,
            definitions: jobsToEnqueue,
            executeAfterDate: clock.UtcNow,
            dbWriteContext: dbWriteContext, 
            transaction: transaction);

        DeleteWorkspace(
            workspaceId,
            dbWriteContext,
            transaction);

        if (Log.IsEnabled(LogEventLevel.Information))
        {
            var fileIds = IdsRange.GroupConsecutiveIds(
                ids: deletedFiles.Select(x => x.Id));

            var fileUploadIds = IdsRange.GroupConsecutiveIds(
                ids: deletedFileUploads.Select(x => x.Id).Concat(deletedCopyFileUploads.Select(x => x.Id)));

            var folderIds = IdsRange.GroupConsecutiveIds(
                ids: deletedFolders);

            var queueJobIds = IdsRange.GroupConsecutiveIds(
                ids: queueJobs.Select(x => x.Value));

            var boxLinkIds = IdsRange.GroupConsecutiveIds(
                ids: deletedBoxLinks);

            var boxIds = IdsRange.GroupConsecutiveIds(
                ids: deletedBoxes.Select(x => x.Id));

            var copyFileQueueJobIds = IdsRange.GroupConsecutiveIds(
                ids: deletedCopyFileQueueJobs.Select(x => x.Id));

            var textractJobIds = IdsRange.GroupConsecutiveIds(
                ids: deletedTextractJobs.Select(x => x.Id));

            Log.Information(
                "Delete workspace#{WorkspaceId} query finished. " +
                "Deleted Folders ({FoldersCount}): {FolderIds}, " +
                "Files ({FilesCount}): [{FileIds}], " +
                "FileUploads ({FileUploadsCount}): [{FileUploadIds}], " +
                "FileUploadParts ({FileUploadPartsCount}), " +
                "BoxLinks ({BoxLinksCount}): [{BoxLinkIds}], " +
                "Boxes ({BoxesCount}): [{BoxIds}]. " +
                "CopyFileQueueJobs ({CopyFileQueueJobsCount}): [{CopyFileQueueJobIds}]" +
                "TextractJobs ({TextractJobsCount}): [{TextractJobIds}]" +
                "Enqueued jobs ({QueueJobsCount}): [{QueueJobIds}]",
                workspaceId,
                deletedFolders.Count,
                folderIds,
                deletedFiles.Count,
                fileIds,
                deletedFileUploads.Count,
                fileUploadIds,
                deletedFileUploadParts.Count + deletedCopyFileUploadParts.Count,
                deletedBoxLinks.Count,
                boxLinkIds,
                deletedBoxes.Count,
                boxIds,
                deletedCopyFileQueueJobs.Count,
                copyFileQueueJobIds,
                deletedTextractJobs.Count,
                textractJobIds,
                queueJobs.Count,
                queueJobIds);
        }

        return new Result(
            Code: ResultCode.Ok,
            WorkspaceExternalId: workspace.Value.ExternalId,
            DeletedBoxes: deletedBoxes.Select(box => box.ExternalId).ToArray());
    }

    private static List<DeletedBoxMembership> DeleteBoxMemberships(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        //todo make sure this query works

        return dbWriteContext
            .Cmd(
                sql: $@"
                    DELETE FROM bm_box_membership
                    WHERE bm_box_id IN (
                        SELECT bo_id
                        FROM bo_boxes
                        WHERE bo_workspace_id = $workspaceId
                    )
                    RETURNING 
                        bm_box_id,
                        bm_member_id,
                        (SELECT u_email FROM u_users WHERE u_id = bm_member_id) AS bm_member_email,
                        (SELECT bo_name FROM bo_boxes WHERE bo_id = bm_box_id) AS bm_box_name
                ",
                readRowFunc: reader => new DeletedBoxMembership(
                    BoxId: reader.GetInt32(0),
                    MemberId: reader.GetInt32(1),
                    MemberEmail: new Email(reader.GetString(2)),
                    BoxName: reader.GetString(3)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }

    private static List<int> DeleteBoxLinks(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        //todo make sure this query works

        return dbWriteContext
            .Cmd(
                sql: $@"
                    DELETE FROM bl_box_links
                    WHERE bl_box_id IN (
                        SELECT bo_id 
                        FROM bo_boxes
                        WHERE bo_workspace_id = $workspaceId
                    )
                    RETURNING 
                        bl_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }
    
    private static List<DeletedBox> DeleteBoxes(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        //todo make sure this query works

        return dbWriteContext
            .Cmd(
                sql: $@"
                    DELETE FROM bo_boxes
                    WHERE bo_workspace_id = $workspaceId
                    RETURNING 
                        bo_id,
                        bo_external_id
                ",
                readRowFunc: reader => new DeletedBox(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<BoxExtId>(1)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }
    
    private static List<int> DeleteFolders(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM fo_folders
                    WHERE fo_workspace_id = $workspaceId                 
                    RETURNING fo_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }

    private static List<DeletedCopyFileQueueJob> DeleteCopyFileQueueJobs(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        //for all jobs which destination workspace is not the deleted one
        //we need to also remove and abort pending file uploads
        //if the destination workspace is the deleted one, then file uploads
        //will be deleted anyway

        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM cfq_copy_file_queue
                    WHERE
                        cfq_source_workspace_id = $workspaceId
                        OR cfq_destination_workspace_id = $workspaceId
                    RETURNING 
                        cfq_id,
                        cfq_file_upload_id,
                        cfq_source_workspace_id,
                        cfq_destination_workspace_id
                ",
                readRowFunc: reader => new DeletedCopyFileQueueJob(
                    Id: reader.GetInt32(0),
                    FileUploadId: reader.GetInt32(1),
                    SourceWorkspaceId: reader.GetInt32(2),
                    DestinationWorkspaceId: reader.GetInt32(3)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }


    private static List<int> GetFileUploadsToDelete(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: @"  
                    SELECT fu_id 
                    FROM fu_file_uploads
                    WHERE fu_workspace_id = $workspaceId
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }
    
    //that method removes all files, so also dependent files (which parentFileId is not null)
    private static List<int> GetFilesToDelete(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: $@"
                    SELECT fi_id 
                    FROM fi_files
                    WHERE fi_workspace_id = $workspaceId        
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }
    
    private static List<DeletedWorkspaceMembership> DeleteWorkspaceMemberships(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        //todo make sure this query works

        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM wm_workspace_membership
                    WHERE wm_workspace_id = $workspaceId
                    RETURNING 
                        wm_member_id,
                        (SELECT u_email FROM u_users WHERE u_id = wm_member_id) AS wm_member_email
                ",
                readRowFunc: reader => new DeletedWorkspaceMembership(
                    MemberId: reader.GetInt32(0),
                    MemberEmail: new Email(reader.GetString(1))),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }
    
    private void DeleteWorkspace(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var deletedWorkspace = dbWriteContext
            .OneRowCmd(
                sql: $@"
                    DELETE FROM w_workspaces
                    WHERE w_id = $workspaceId
                    RETURNING w_id                        
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        if (deletedWorkspace.IsEmpty)
        {
            Log.Warning("Workspace '{WorkspaceId}' was not deleted because it was not found", workspaceId);
        }
    }
    
    private readonly record struct DeletedBoxMembership(
        int BoxId,
        int MemberId,
        Email MemberEmail,
        string BoxName);
    
    private readonly record struct DeletedBox(
        int Id,
        BoxExtId ExternalId);
    
    private readonly record struct DeletedWorkspaceMembership(
        int MemberId,
        Email MemberEmail);
    
    public readonly record struct Result(
        ResultCode Code,
        WorkspaceExtId WorkspaceExternalId = default,
        BoxExtId[]? DeletedBoxes = default);
    
    public enum ResultCode
    {
        Ok,
        WorkspaceNotFound
    }

    private readonly record struct DeletedCopyFileQueueJob(
        int Id,
        int FileUploadId,
        int SourceWorkspaceId,
        int DestinationWorkspaceId);
}