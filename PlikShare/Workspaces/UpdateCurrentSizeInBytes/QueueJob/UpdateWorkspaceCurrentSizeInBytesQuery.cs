using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;

public static class UpdateWorkspaceCurrentSizeInBytesQuery
{
    public static Result Execute(
        int workspaceId,
        long currentSizeInBytes,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE w_workspaces
                     SET w_current_size_in_bytes = $currentSizeInBytes
                     WHERE w_id = $workspaceId
                     RETURNING w_external_id
                     """,
                readRowFunc: reader => reader.GetExtId<WorkspaceExtId>(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$currentSizeInBytes", currentSizeInBytes)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Workspace#{WorkspaceId} current size in bytes because it was not found.",
                workspaceId);

            return new Result(
                Code: ResultCode.WorkspaceNotFound);
        }

        Log.Debug("Workspace#{WorkspaceId} current size in bytes was updated to {CurrentSizeInBytes} bytes.",
            workspaceId,
            currentSizeInBytes);

        return new Result(
            Code: ResultCode.Ok,
            WorkspaceExternalId: result.Value);
    }

    public enum ResultCode
    {
        Ok = 0,
        WorkspaceNotFound
    }
    
    public readonly record struct Result(
        ResultCode Code,
        WorkspaceExtId WorkspaceExternalId = default);
}