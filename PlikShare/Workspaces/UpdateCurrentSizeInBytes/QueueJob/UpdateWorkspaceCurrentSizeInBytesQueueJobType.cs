using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using Serilog;

namespace PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;

public static class UpdateWorkspaceCurrentSizeInBytesQueueJobType
{
    public const string Value = "update_workspace_current_size_in_bytes";
}

public static class WorkspaceSizeUpdateQueueExtensions
{
    public static QueueJobId EnqueueWorkspaceSizeUpdateJob(
        this IQueue queue,
        IClock clock,
        int workspaceId,
        Guid correlationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var queueJobId =  queue.EnqueueOrThrow(
            correlationId: correlationId,
            jobType: UpdateWorkspaceCurrentSizeInBytesQueueJobType.Value,
            definition: new UpdateWorkspaceCurrentSizeInBytesQueueJobDefinition
            {
                WorkspaceId= workspaceId
            },

            //one second of delay + debounceId not to recalculate this
            //all over again when many files are being uploaded at once
            executeAfterDate: clock.UtcNow.AddSeconds(1), 
            debounceId: $"update_workspace_current_size_in_bytes_{workspaceId}",

            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        Log.Debug("Workspace#{WorkspaceId} size update job enqueued (QueueJob#{QueueJobId})", 
            workspaceId, 
            queueJobId);

        return queueJobId;
    }
}