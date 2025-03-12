using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Boxes.Delete.QueueJob;

public class DeleteBoxesQueueJobExecutor(
    BatchDeleteBoxesWithDependenciesQuery batchDeleteBoxesWithDependenciesQuery,
    BoxCache boxCache,
    BoxMembershipCache boxMembershipCache) : IQueueDbOnlyJobExecutor
{
    public string JobType => DeleteBoxesQueueJobType.Value;
    public int Priority => QueueJobPriority.Low;

    public (QueueJobResult Result, Func<CancellationToken, ValueTask> SideEffectsToRun) Execute(
        string definitionJson, 
        Guid correlationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var definition = Json.Deserialize<DeleteBoxesQueueJobDefinition>(
            json: definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DeleteBoxesQueueJobExecutor)}'");
        }
        
        var result = batchDeleteBoxesWithDependenciesQuery.Execute(
            workspaceId: definition.WorkspaceId,
            boxIds: definition.BoxIds,
            correlationId: correlationId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);
       
        Log.Information("Boxes '{BoxIds}' in Workspace '{WorkspaceId}' was deleted.",
            definition.BoxIds,
            definition.WorkspaceId);

        return (
            Result: QueueJobResult.Success, 
            SideEffectsToRun: cancellationToken => ClearCaches(result, cancellationToken)
        );
    }

    private async ValueTask ClearCaches(
        BatchDeleteBoxesWithDependenciesQuery.Result result,
        CancellationToken cancellationToken)
    {
        foreach (var deletedBoxExtId in result.DeletedBoxes)
        {
            await boxCache.InvalidateEntry(
                boxExternalId: deletedBoxExtId,
                cancellationToken: cancellationToken);
        }

        foreach (var deletedMember in result.DeletedBoxMembers)
        {
            await boxMembershipCache.InvalidateEntry(
                boxExternalId: deletedMember.BoxExternalId,
                memberId: deletedMember.MemberId,
                cancellationToken: cancellationToken);
        }
    }
}