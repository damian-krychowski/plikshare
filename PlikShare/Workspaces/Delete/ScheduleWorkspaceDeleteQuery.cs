using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Delete.QueueJob;
using PlikShare.Workspaces.Id;
using Serilog;
using Serilog.Events;

namespace PlikShare.Workspaces.Delete;

public class ScheduleWorkspaceDeleteQuery(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        WorkspaceContext workspace,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        Guid correlationId)
    {
        if (IsWorkspaceUsedByIntegration(workspace, dbWriteContext))
            return new Result(Code: ResultCode.UsedByIntegration);

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var markWorkspaceToDeleteResult = MarkWorkspaceToDelete(
                workspace,
                dbWriteContext, 
                transaction);

            if (markWorkspaceToDeleteResult.IsEmpty)
            {
                transaction.Rollback();
                
                Log.Warning("Could not delete Workspace#{WorkspaceId} because it was not found.",
                    workspace.Id);

                return new Result(
                    Code: ResultCode.NotFound);
            }
            
            var foldersMarkedToDelete = MarkFoldersToDelete(
                workspace,
                dbWriteContext, 
                transaction);
            
            var boxesMarkedToDelete = MarkBoxesToDelete(
                workspace,
                dbWriteContext,
                transaction);

            var deleteWorkspaceJob = queue.EnqueueOrThrow(
                jobType: DeleteWorkspaceQueueJobType.Value,
                correlationId: correlationId,
                definition: new DeleteWorkspaceQueueJobDefinition
                {
                    WorkspaceId = workspace.Id,
                    DeletedAt = clock.UtcNow
                },
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
                            
            transaction.Commit();

            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var folderIds = IdsRange.GroupConsecutiveIds(
                    ids: foldersMarkedToDelete);

                var boxIds = IdsRange.GroupConsecutiveIds(
                    ids: boxesMarkedToDelete);

                Log.Information(
                    "Workspace#{WorkspaceId} was scheduled to be deleted. " +
                    "Folders affected ({FoldersCount}): {FolderIds}, " +
                    "Boxes affected ({BoxesCount}): {BoxIds}. " +
                    "Delete-workspace queue job scheduled '{QueueJobId}'",
                    workspace.Id,
                    foldersMarkedToDelete.Count,
                    folderIds,
                    boxesMarkedToDelete.Count,
                    boxIds,
                    deleteWorkspaceJob.Value);
            }


            return new Result(
                Code: ResultCode.Ok,
                DeletedBoxes: boxesMarkedToDelete);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while scheduling Workspace#{WorkspaceId} to be deleted", 
                workspace.Id);

            throw;
        }
    }

    private bool IsWorkspaceUsedByIntegration(
        WorkspaceContext workspace,
        DbWriteQueue.Context dbWriteContext)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: @"
                    SELECT EXISTS (       
                        SELECT 1
                        FROM i_integrations
                        WHERE i_workspace_id = $workspaceId
                    )
                ",
                readRowFunc: reader => reader.GetBoolean(0))
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        return result is
        {
            IsEmpty: false, 
            Value: true
        };
    }

    private static SQLiteOneRowCommandResult<WorkspaceExtId> MarkWorkspaceToDelete(
        WorkspaceContext workspace,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: @"
                        UPDATE w_workspaces
                        SET w_is_being_deleted = TRUE
                        WHERE w_id = $workspaceId
                        RETURNING w_external_id
                    ",
                readRowFunc: reader => reader.GetExtId<WorkspaceExtId>(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private static List<int> MarkFoldersToDelete(
        WorkspaceContext workspace,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: @"
                        UPDATE fo_folders
                        SET fo_is_being_deleted = TRUE
                        WHERE fo_workspace_id = $workspaceId
                        RETURNING fo_id;
                    ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private static List<int> MarkBoxesToDelete(
        WorkspaceContext workspace,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: @"
                    UPDATE bo_boxes
                    SET bo_is_being_deleted = TRUE
                    WHERE bo_workspace_id = $workspaceId
                    RETURNING bo_id;
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }


    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        UsedByIntegration
    }

    public readonly record struct Result(
        ResultCode Code,
        List<int>? DeletedBoxes = default);
}