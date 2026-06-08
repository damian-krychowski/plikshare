using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.Delete.QueueJob;

public class DeleteWorkspaceQueueJobExecutor(
    DbWriteQueue dbWriteQueue,
    DeleteWorkspaceWithDependenciesQuery deleteWorkspaceWithDependenciesQuery,
    BoxCache boxCache,
    UserCache userCache,
    WorkspaceCache workspaceCache,
    WorkspaceSizeCache workspaceSizeCache) : IQueueNormalJobExecutor
{
    public static string StaticJobType => DeleteWorkspaceQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.Normal;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
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

        var result = await dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                using var transaction = context.Connection.BeginTransaction();

                try
                {
                    var deleteResult = deleteWorkspaceWithDependenciesQuery.Execute(
                        workspaceId: definition.WorkspaceId,
                        deletedAt: definition.DeletedAt,
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

        workspaceSizeCache.Forget(
            workspaceId: definition.WorkspaceId);

        if (result.Code == DeleteWorkspaceWithDependenciesQuery.ResultCode.WorkspaceNotFound)
            return QueueJobResult.Success;

        await ClearCaches(result, cancellationToken);

        return QueueJobResult.Success;
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

        // Wek rows for every member were dropped by the workspace delete; UserCache mirrors
        // those in WrappedWorkspaceDeks, so each affected member needs invalidation.
        foreach (var memberId in result.DeletedMemberIds!)
        {
            await userCache.InvalidateEntry(
                userId: memberId,
                cancellationToken: cancellationToken);
        }
    }
}
