using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.CreateBucket;

public class UpdateWorkspaceIsBucketCreatedQuery(
    DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        int workspaceId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspaceId: workspaceId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        int workspaceId)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE w_workspaces
                    SET w_is_bucket_created = TRUE
                    WHERE w_id = $workspaceId
                    RETURNING w_id
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
        
        if (result.IsEmpty)
        {
            Log.Warning("Could not update is_bucket_created of Workspace '{WorkspaceId}' because it was not found.",
                workspaceId);

            return ResultCode.NotFound;
        }

        Log.Information("Workspace '{WorkspaceId}' is_bucket_created field was updated.", 
            workspaceId);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}