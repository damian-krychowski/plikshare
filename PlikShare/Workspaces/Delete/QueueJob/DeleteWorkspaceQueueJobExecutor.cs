using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.Delete.QueueJob;

public class DeleteWorkspaceQueueJobExecutor(
    DeleteWorkspaceWithDependenciesQuery deleteWorkspaceWithDependenciesQuery,
    BoxCache boxCache,
    WorkspaceCache workspaceCache) : IQueueDbOnlyJobExecutor
{
    public string JobType => DeleteWorkspaceQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

    public (QueueJobResult Result, Func<CancellationToken, ValueTask> SideEffectsToRun) Execute(
        string definitionJson,
        Guid correlationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {   
        var definition = Json.Deserialize<DeleteWorkspaceQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DeleteWorkspaceQueueJobDefinition)}'");
        }

        Log.Information("Workspace#{WorkspaceId} delete operation started",
            definition.WorkspaceId);
        
        var result = deleteWorkspaceWithDependenciesQuery.Execute(
            workspaceId: definition.WorkspaceId,
            deletedAt: definition.DeletedAt,
            correlationId: correlationId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        if (result.Code == DeleteWorkspaceWithDependenciesQuery.ResultCode.WorkspaceNotFound)
            return (
                Result: QueueJobResult.Success, 
                SideEffectsToRun: _ => ValueTask.CompletedTask
            );
        
        return (
            Result: QueueJobResult.Success, 
            SideEffectsToRun: token => ClearCaches(result, token)
        );
    }

    private async ValueTask ClearCaches(
        DeleteWorkspaceWithDependenciesQuery.Result result,
        CancellationToken cancellationToken)
    {
        await workspaceCache.InvalidateEntry(
            workspaceExternalId: result.WorkspaceExternalId,
            cancellationToken: cancellationToken);
        
        foreach (var deletedBox in result.DeletedBoxes!)
        {
            await boxCache.InvalidateEntry(
                boxExternalId: deletedBox,
                cancellationToken: cancellationToken);
        }
    }
}