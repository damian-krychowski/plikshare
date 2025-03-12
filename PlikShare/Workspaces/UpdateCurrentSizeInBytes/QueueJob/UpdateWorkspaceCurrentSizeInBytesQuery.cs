using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;

public class UpdateWorkspaceCurrentSizeInBytesQuery
{
    public Result Execute(
        int workspaceId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE w_workspaces
                    SET w_current_size_in_bytes =  COALESCE((
                        SELECT SUM(fi_size_in_bytes)
                        FROM fi_files
                        WHERE fi_workspace_id = $workspaceId
                    ), 0)
                    WHERE w_id = $workspaceId
                    RETURNING
                        w_external_id, 
                        w_current_size_in_bytes
                ",
                readRowFunc: reader => new QueryResult(
                    ExternalId: reader.GetExtId<WorkspaceExtId>(0),
                    CurrentSizeInBytes: reader.GetInt64(1)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Workspace '{WorkspaceId}' current size in bytes because it was not found.",
                workspaceId);

            return new Result(
                Code: ResultCode.WorkspaceNotFound);
        }

        Log.Information("Workspace '{WorkspaceId}' current size in bytes was updated." +
                        "Result: {@QueryResult}",
            workspaceId,
            result.Value);

        return new Result(
            Code: ResultCode.Ok,
            WorkspaceExternalId: result.Value.ExternalId);
    }

    public enum ResultCode
    {
        Ok = 0,
        WorkspaceNotFound
    }
    
    public readonly record struct Result(
        ResultCode Code,
        WorkspaceExtId WorkspaceExternalId = default);
    
    private readonly record struct QueryResult(
        WorkspaceExtId ExternalId,
        long CurrentSizeInBytes);
}