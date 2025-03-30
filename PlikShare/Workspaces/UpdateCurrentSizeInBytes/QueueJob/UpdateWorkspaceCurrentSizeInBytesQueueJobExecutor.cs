using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.GetSize;
using Serilog;

namespace PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;

public class UpdateWorkspaceCurrentSizeInBytesQueueJobExecutor(
    WorkspaceCache workspaceCache) : IQueueDbOnlyJobExecutor
{
    public string JobType => UpdateWorkspaceCurrentSizeInBytesQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

    public (QueueJobResult Result, Func<CancellationToken, ValueTask> SideEffectsToRun) Execute(
        string definitionJson, 
        Guid correlationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var definition = Json.Deserialize<UpdateWorkspaceCurrentSizeInBytesQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(UpdateWorkspaceCurrentSizeInBytesQueueJobDefinition)}'");
        }
        
        Log.Debug("Workspace#{WorkspaceId} current size in bytes update started.",
            definition.WorkspaceId);

        var currentWorkspaceSizeInBytes = GetWorkspaceSizeQuery.Execute(
            definition.WorkspaceId,
            dbWriteContext,
            transaction);

        var result = UpdateWorkspaceCurrentSizeInBytesQuery.Execute(
            workspaceId: definition.WorkspaceId,
            currentSizeInBytes: currentWorkspaceSizeInBytes,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        if (result.Code == UpdateWorkspaceCurrentSizeInBytesQuery.ResultCode.WorkspaceNotFound)
            return (
                Result: QueueJobResult.Success, 
                SideEffectsToRun: _ => ValueTask.CompletedTask
            );

        return (
            Result: QueueJobResult.Success, 
            SideEffectsToRun: token => ClearCache(result, token)
        );
    }

    private async ValueTask ClearCache(
        UpdateWorkspaceCurrentSizeInBytesQuery.Result result,
        CancellationToken cancellationToken)
    {
        await workspaceCache.InvalidateEntry(
            result.WorkspaceExternalId,
            cancellationToken: cancellationToken);
    }
}