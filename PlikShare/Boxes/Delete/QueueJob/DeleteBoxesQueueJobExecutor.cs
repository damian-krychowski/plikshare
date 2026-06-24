using PlikShare.Agents.BoxAccess;
using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Boxes.Delete.QueueJob;

public class DeleteBoxesQueueJobExecutor(
    DbWriteQueue dbWriteQueue,
    BatchDeleteBoxesWithDependenciesQuery batchDeleteBoxesWithDependenciesQuery,
    BoxCache boxCache,
    BoxMembershipCache boxMembershipCache,
    AgentBoxAccessCache agentBoxAccessCache) : IQueueNormalJobExecutor
{
    public static string StaticJobType => DeleteBoxesQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.Low;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<DeleteBoxesQueueJobDefinition>(
            json: definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DeleteBoxesQueueJobExecutor)}'");
        }

        var result = await dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                using var transaction = context.Connection.BeginTransaction();

                try
                {
                    var deleteResult = batchDeleteBoxesWithDependenciesQuery.Execute(
                        workspaceId: definition.WorkspaceId,
                        boxIds: definition.BoxIds,
                        correlationId: correlationId,
                        dbWriteContext: context,
                        transaction: transaction);

                    transaction.Commit();

                    return deleteResult;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            },
            cancellationToken: cancellationToken);

        Log.Information("Boxes '{BoxIds}' in Workspace#{WorkspaceId} was deleted.",
            definition.BoxIds,
            definition.WorkspaceId);

        await ClearCaches(result, cancellationToken);

        foreach (var boxId in definition.BoxIds)
        {
            await agentBoxAccessCache.InvalidateAllForBox(
                boxId,
                cancellationToken);
        }

        return QueueJobResult.Success;
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
                boxId: deletedMember.BoxId,
                memberId: deletedMember.MemberId,
                cancellationToken: cancellationToken);
        }
    }
}
