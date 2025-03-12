using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.OpenAi.ChatGpt;
using PlikShare.Storages;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Workspaces.Cache;

public class WorkspaceCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    UserCache userCache,
    StorageClientStore storageClientStore,
    TextractClientStore textractClientStore,
    ChatGptClientStore chatGptClientStore)
{
    private readonly ConcurrentDictionary<int, WorkspaceExtId> _idExtIdMap = new();

    private static string WorkspaceExternalIdKey(WorkspaceExtId externalId) => $"workspace:external-id:{externalId}";
    
    public async ValueTask<WorkspaceContext?> TryGetWorkspace(
        WorkspaceExtId workspaceExternalId,
        CancellationToken cancellationToken)
    {
        var workspaceCached = await cache.GetOrCreateAsync(
            key: WorkspaceExternalIdKey(workspaceExternalId),
            factory: _ => ValueTask.FromResult(LoadWorkspace(workspaceExternalId)),
            cancellationToken: cancellationToken);

        if (workspaceCached is null)
            return null;

        var owner = await userCache.TryGetUser(
            userId: workspaceCached.OwnerId,
            cancellationToken: cancellationToken);

        if (owner is null)
        {
            throw new InvalidOperationException(
                $"Owner of workspace '{workspaceExternalId}' was not found.");
        }

        if(!storageClientStore.TryGetClient(storageId: workspaceCached.StorageId,  out var storageClient))
        {
            throw new InvalidOperationException(
                $"Storage#{workspaceCached.StorageId} of workspace '{workspaceExternalId}' was not found.");
        }

        var textractClient = textractClientStore.TryGetClient(
            workspaceId: workspaceCached.Id,
            storageId: workspaceCached.StorageId);

        var chatGptClients = chatGptClientStore.GetClients();

        return new WorkspaceContext
        {
            Id = workspaceCached.Id,
            ExternalId = workspaceCached.ExternalId,
            Name = workspaceCached.Name,
            CurrentSizeInBytes = workspaceCached.CurrentSizeInBytes,
            BucketName = workspaceCached.BucketName,
            IsBucketCreated = workspaceCached.IsBucketCreated,
            IsBeingDeleted = workspaceCached.IsBeingDeleted,
            Owner = owner,
            Storage = storageClient,

            Integrations = new WorkspaceIntegrations
            {
                Textract = textractClient,
                ChatGpt = chatGptClients
            }
        };
    }

    private WorkspaceCached? LoadWorkspace(
        WorkspaceExtId workspaceExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, workspace) = connection
            .OneRowCmd(
                sql: @"
                    SELECT
		                w_id,
		                w_external_id,		                
		                w_owner_id,
		                w_name,
		                w_current_size_in_bytes,
		                w_bucket_name,
		                w_is_bucket_created,
		                w_is_being_deleted,
		                w_storage_id
	                FROM w_workspaces
	                WHERE w_external_id = $workspaceExternalId
			        LIMIT 1
                ",
                readRowFunc: reader => new WorkspaceCached(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<WorkspaceExtId>(1),
                    OwnerId: reader.GetInt32(2),
                    Name: reader.GetString(3),
                    CurrentSizeInBytes: reader.GetInt64(4),
                    BucketName: reader.GetString(5),
                    IsBucketCreated: reader.GetBoolean(6),
                    IsBeingDeleted: reader.GetBoolean(7),
                    StorageId: reader.GetInt32(8)))
            .WithParameter("$workspaceExternalId", workspaceExternalId.Value)
            .Execute();

        if (isEmpty)
            return null;

        UpdateIdMap(
            workspaceId: workspace.Id,
            workspaceExternalId: workspace.ExternalId);

        return workspace;
    }

    public async ValueTask<WorkspaceContext?> TryGetWorkspace(
        int workspaceId,
        CancellationToken cancellationToken)
    {
        if (_idExtIdMap.TryGetValue(workspaceId, out var externalId))
            return await TryGetWorkspace(externalId, cancellationToken);
        
        using var connection = plikShareDb.OpenConnection();
        
        var (isEmpty, workspace) = connection
            .OneRowCmd(
                sql: @"
                    SELECT
		                w_id,
		                w_external_id,		                
		                w_owner_id,
		                w_name,
		                w_current_size_in_bytes,
		                w_bucket_name,
		                w_is_bucket_created,
		                w_is_being_deleted,
		                w_storage_id
	                FROM w_workspaces
	                WHERE w_id = $workspaceId
			        LIMIT 1
                ",
                readRowFunc: reader => new WorkspaceCached(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<WorkspaceExtId>(1),
                    OwnerId: reader.GetInt32(2),
                    Name: reader.GetString(3),
                    CurrentSizeInBytes: reader.GetInt64(4),
                    BucketName: reader.GetString(5),
                    IsBucketCreated: reader.GetBoolean(6),
                    IsBeingDeleted: reader.GetBoolean(7),
                    StorageId: reader.GetInt32(8)))
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        if (isEmpty)
            return null;

        UpdateIdMap(
            workspaceId: workspace.Id,
            workspaceExternalId: workspace.ExternalId);
        
        await cache.SetAsync(
            key: WorkspaceExternalIdKey(workspace.ExternalId),
            value: workspace,
            cancellationToken: cancellationToken);

        var owner = await userCache.TryGetUser(
            userId: workspace.OwnerId,
            cancellationToken: cancellationToken);

        if (owner is null)
        {
            throw new InvalidOperationException(
                $"Owner of workspace '{workspace.ExternalId}' was not found.");
        }

        if (!storageClientStore.TryGetClient(storageId: workspace.StorageId, out var storageClient))
        {
            throw new InvalidOperationException(
                $"Storage#{workspace.StorageId} of workspace '{workspace.ExternalId}' was not found.");
        }
        
        var textractClient = textractClientStore.TryGetClient(
            workspaceId: workspace.Id,
            storageId: workspace.StorageId);
        
        var chatGptClients = chatGptClientStore.GetClients();

        return new WorkspaceContext
        {
            Id = workspace.Id,
            ExternalId = workspace.ExternalId,
            Name = workspace.Name,
            CurrentSizeInBytes = workspace.CurrentSizeInBytes,
            BucketName = workspace.BucketName,
            IsBucketCreated = workspace.IsBucketCreated,
            IsBeingDeleted = workspace.IsBeingDeleted,
            Owner = owner,
            Storage = storageClient,

            Integrations = new WorkspaceIntegrations
            {
                Textract = textractClient,
                ChatGpt = chatGptClients
            }
        };
    }

    public ValueTask InvalidateEntry(
        WorkspaceExtId workspaceExternalId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            WorkspaceExternalIdKey(workspaceExternalId),
            cancellationToken);
    }
    
    public async ValueTask InvalidateEntry(
        int workspaceId,
        CancellationToken cancellationToken)
    {
        if (_idExtIdMap.Remove(workspaceId, out var workspaceExternalId))
        {
            await cache.RemoveAsync(
                WorkspaceExternalIdKey(workspaceExternalId),
                cancellationToken);
        }
    }
    
    private void UpdateIdMap(
        int workspaceId,
        WorkspaceExtId workspaceExternalId)
    {
        _idExtIdMap.AddOrUpdate(
            key: workspaceId,
            addValueFactory: _ => workspaceExternalId,
            updateValueFactory: (_, _) => workspaceExternalId);
    }

    [ImmutableObject(true)]
    public sealed record WorkspaceCached(
        int Id,
        WorkspaceExtId ExternalId,
        int OwnerId,
        string Name,
        long CurrentSizeInBytes,
        string BucketName,
        bool IsBucketCreated,
        bool IsBeingDeleted,
        int StorageId);
}