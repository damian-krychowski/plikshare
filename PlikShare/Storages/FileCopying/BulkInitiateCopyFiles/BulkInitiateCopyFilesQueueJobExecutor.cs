using PlikShare.Core.Queue;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Uploads.Initiate;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Storages.FileCopying.BulkInitiateCopyFiles;

public class BulkInitiateCopyFilesQueueJobExecutor(
    WorkspaceCache workspaceCache,
    BulkInitiateCopyFileUploadOperation bulkInitiateCopyFileUploadOperation) : IQueueNormalJobExecutor
{
    public string JobType => BulkInitiateCopyFilesQueueJobType.Value;
    public int Priority => QueueJobPriority.High;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<BulkInitiateCopyFilesQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(BulkInitiateCopyFilesQueueJobDefinition)}'");
        }

        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId: definition.DestinationWorkspaceId,
            cancellationToken: cancellationToken);

        if (workspace is null)
        {
            //todo add log
            return QueueJobResult.Success;
        }

        await bulkInitiateCopyFileUploadOperation.Execute(
            destinationWorkspace: workspace,
            definition: definition,
            userIdentity: new GenericUserIdentity(
                IdentityType: definition.UserIdentityType,
                Identity: definition.UserIdentity), 
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        return QueueJobResult.Success;
    }
}