using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.GetSize;
using Serilog;

namespace PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;

public class UpdateWorkspaceCurrentSizeInBytesQueueJobExecutor(
    GetWorkspaceSizeQuery getWorkspaceSizeQuery,
    WorkspaceSizeCache workspaceSizeCache) : IQueueNormalJobExecutor
{
    public static string StaticJobType => UpdateWorkspaceCurrentSizeInBytesQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.Normal;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
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

        var currentWorkspaceSizeInBytes = getWorkspaceSizeQuery.Execute(
            workspaceId: definition.WorkspaceId);

        workspaceSizeCache.Set(
            workspaceId: definition.WorkspaceId,
            sizeInBytes: currentWorkspaceSizeInBytes);

        return Task.FromResult(QueueJobResult.SuccessWithDbWrite(
            dbWrite: (dbWriteContext, transaction) => UpdateWorkspaceCurrentSizeInBytesQuery.Execute(
                workspaceId: definition.WorkspaceId,
                currentSizeInBytes: currentWorkspaceSizeInBytes,
                dbWriteContext: dbWriteContext,
                transaction: transaction)));
    }
}
