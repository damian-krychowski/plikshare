using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.OpenAi.ChatGpt;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
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
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string WorkspaceIdKey(int id) => $"workspace:id:{id}";
    private static string WorkspaceExtIdKey(WorkspaceExtId extId) => $"workspace:extid:{extId.Value}";
    private static string WorkspaceTag(int id) => $"workspace-{id}";

    public async ValueTask<WorkspaceContext?> TryGetWorkspace(
        int workspaceId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeWorkspaceCache(
            WorkspaceIdKey(workspaceId),
            cancellationToken);

        if (cached is not null)
            return await BuildContext(
                cached, 
                cancellationToken);

        var workspace = LoadWorkspace(
            WorkspaceLookup.ById(workspaceId));

        if (workspace is null)
            return null;

        await StoreInAllKeys(
            workspace, 
            cancellationToken);

        return await BuildContext(
            workspace, 
            cancellationToken);
    }

    public async ValueTask<WorkspaceContext?> TryGetWorkspace(
        WorkspaceExtId workspaceExternalId,
        CancellationToken cancellationToken)
    {
        // Step 1: resolve ExtId → int Id via pointer
        var workspaceId = await cache.GetOrCreateAsync<int?>(
            key: WorkspaceExtIdKey(workspaceExternalId),
            factory: _ => ValueTask.FromResult<int?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);

        if (workspaceId is not null)
        {
            // Step 2: delegate to the hot path
            return await TryGetWorkspace(
                workspaceId.Value, 
                cancellationToken);
        }

        // Pointer not in cache — load from DB
        var workspace = LoadWorkspace(
            WorkspaceLookup.ByExternalId(workspaceExternalId));

        if (workspace is null)
            return null;

        await StoreInAllKeys(
            workspace, 
            cancellationToken);

        return await BuildContext(
            workspace, 
            cancellationToken);
    }

    private ValueTask<WorkspaceCached?> ProbeWorkspaceCache(
        string key,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync<WorkspaceCached?>(
            key: key,
            factory: _ => ValueTask.FromResult<WorkspaceCached?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask StoreInAllKeys(
        WorkspaceCached workspace,
        CancellationToken cancellationToken)
    {
        var tags = new[] { WorkspaceTag(workspace.Id) };

        // Primary key — full data
        await cache.SetAsync(
            WorkspaceIdKey(workspace.Id),
            workspace,
            tags: tags,
            cancellationToken: cancellationToken);

        // Pointer: ExtId → int Id
        await cache.SetAsync(
            WorkspaceExtIdKey(workspace.ExternalId),
            workspace.Id,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<WorkspaceContext> BuildContext(
        WorkspaceCached workspace,
        CancellationToken cancellationToken)
    {
        var owner = await userCache.TryGetUser(
            userId: workspace.OwnerId,
            cancellationToken: cancellationToken);

        if (owner is null)
        {
            throw new InvalidOperationException(
                $"Owner of workspace '{workspace.ExternalId}' was not found.");
        }

        if (!storageClientStore.TryGetClient(
                storageId: workspace.StorageId,
                out var storageClient))
        {
            throw new InvalidOperationException(
                $"Storage#{workspace.StorageId} of workspace '{workspace.ExternalId}' was not found.");
        }

        var integrations = PrepareWorkspaceIntegrations(
            workspace, 
            storageClient);

        return new WorkspaceContext
        {
            Id = workspace.Id,
            ExternalId = workspace.ExternalId,
            Name = workspace.Name,
            CurrentSizeInBytes = workspace.CurrentSizeInBytes,
            MaxSizeInBytes = workspace.MaxSizeInBytes,
            MaxTeamMembers = workspace.MaxTeamMembers,
            BucketName = workspace.BucketName,
            IsBucketCreated = workspace.IsBucketCreated,
            IsBeingDeleted = workspace.IsBeingDeleted,
            Owner = owner,
            Storage = storageClient,
            EncryptionMetadata = workspace.EncryptionMetadata,
            Integrations = integrations
        };
    }

    private WorkspaceIntegrations PrepareWorkspaceIntegrations(
        WorkspaceCached workspace, 
        IStorageClient storageClient)
    {
        // Full-encrypted storages must not expose any third-party integration: the server cannot
        // read file contents, so Textract/ChatGPT can't operate, and silently returning an
        // unrelated client (TextractClientStore falls back to any registered client) would surface
        // bogus actions in the UI and leak cross-storage integration presence.
        var isFullyEncrypted = storageClient.Encryption.Type == StorageEncryptionType.Full;

        var textractClient = isFullyEncrypted
            ? null
            : textractClientStore.TryGetClient(
                workspaceId: workspace.Id,
                storageId: workspace.StorageId);

        var chatGptClients = isFullyEncrypted
            ? []
            : chatGptClientStore.GetClients();

        var integrations = new WorkspaceIntegrations
        {
            Textract = textractClient,
            ChatGpt = chatGptClients
        };

        return integrations;
    }

    public ValueTask InvalidateEntry(
        int workspaceId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            WorkspaceTag(workspaceId),
            cancellationToken);
    }

    public async ValueTask InvalidateEntry(
        WorkspaceExtId workspaceExternalId,
        CancellationToken cancellationToken)
    {
        var workspaceId = await cache.GetOrCreateAsync(
            key: WorkspaceExtIdKey(workspaceExternalId),
            factory: _ => ValueTask.FromResult<int?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);

        if (workspaceId is not null)
        {
            await InvalidateEntry(workspaceId.Value, cancellationToken);
        }
        else
        {
            // Fallback: drop the pointer key if the workspace is gone from the DB.
            await cache.RemoveAsync(
                WorkspaceExtIdKey(workspaceExternalId),
                cancellationToken);
        }
    }

    private WorkspaceCached? LoadWorkspace(WorkspaceLookup lookup)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: $"""
                     SELECT
                         w_id,
                         w_external_id,
                         w_owner_id,
                         w_name,
                         w_current_size_in_bytes,
                         w_max_size_in_bytes,
                         w_max_team_members,
                         w_bucket_name,
                         w_is_bucket_created,
                         w_is_being_deleted,
                         w_storage_id,
                         w_encryption_salt
                     FROM w_workspaces
                     WHERE {lookup.WhereClause}
                     LIMIT 1
                     """,
                readRowFunc: reader =>
                {
                    var salt = reader.GetFieldValueOrNull<byte[]>(11);

                    return new WorkspaceCached
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetExtId<WorkspaceExtId>(1),
                        OwnerId = reader.GetInt32(2),
                        Name = reader.GetString(3),
                        CurrentSizeInBytes = reader.GetInt64(4),
                        MaxSizeInBytes = reader.GetInt64OrNull(5),
                        MaxTeamMembers = reader.GetInt32OrNull(6),
                        BucketName = reader.GetString(7),
                        IsBucketCreated = reader.GetBoolean(8),
                        IsBeingDeleted = reader.GetBoolean(9),
                        StorageId = reader.GetInt32(10),
                        EncryptionMetadata = salt is null
                            ? null
                            : new WorkspaceEncryptionMetadata { Salt = salt }
                    };
                })
            .WithParameter(lookup.ParamName, lookup.ParamValue)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }
    private readonly record struct WorkspaceLookup(
        string WhereClause,
        string ParamName,
        object ParamValue)
    {
        public static WorkspaceLookup ById(int id) =>
            new("w_id = $workspaceId", "$workspaceId", id);

        public static WorkspaceLookup ByExternalId(WorkspaceExtId extId) =>
            new("w_external_id = $workspaceExternalId", "$workspaceExternalId", extId.Value);
    }

    [ImmutableObject(true)]
    public sealed class WorkspaceCached
    {
        public required int Id { get; init; }
        public required WorkspaceExtId ExternalId { get; init; }
        public required int OwnerId { get; init; }
        public required string Name { get; init; }
        public required long CurrentSizeInBytes { get; init; }
        public required long? MaxSizeInBytes { get; init; }
        public required int? MaxTeamMembers { get; init; }
        public required string BucketName { get; init; }
        public required bool IsBucketCreated { get; init; }
        public required bool IsBeingDeleted { get; init; }
        public required int StorageId { get; init; }
        public required WorkspaceEncryptionMetadata? EncryptionMetadata { get; init; }
    }
}