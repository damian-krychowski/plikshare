using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Boxes.Cache;

public class BoxCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    WorkspaceCache workspaceCache)
{
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string BoxIdKey(int id) => $"box:id:{id}";
    private static string BoxExtIdKey(BoxExtId extId) => $"box:extid:{extId.Value}";
    private static string BoxTag(int id) => $"box-{id}";

    public async ValueTask<BoxContext?> TryGetBox(
        int boxId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeBoxCache(
            BoxIdKey(boxId),
            cancellationToken);

        if (cached is not null)
            return await BuildContext(cached, cancellationToken);

        var box = LoadBox(BoxLookup.ById(boxId));

        if (box is null)
            return null;

        await StoreInAllKeys(box, cancellationToken);
        return await BuildContext(box, cancellationToken);
    }

    public async ValueTask<BoxContext?> TryGetBox(
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        // Step 1: resolve ExtId → int Id via pointer
        var boxId = await cache.GetOrCreateAsync<int?>(
            key: BoxExtIdKey(boxExternalId),
            factory: _ => ValueTask.FromResult<int?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);

        if (boxId is not null)
        {
            // Step 2: delegate to the hot path
            return await TryGetBox(boxId.Value, cancellationToken);
        }

        // Pointer not in cache — load from DB
        var box = LoadBox(BoxLookup.ByExternalId(boxExternalId));

        if (box is null)
            return null;

        await StoreInAllKeys(box, cancellationToken);
        return await BuildContext(box, cancellationToken);
    }

    private ValueTask<BoxCached?> ProbeBoxCache(
        string key,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync<BoxCached?>(
            key: key,
            factory: _ => ValueTask.FromResult<BoxCached?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask StoreInAllKeys(
        BoxCached box,
        CancellationToken cancellationToken)
    {
        var tags = new[] { BoxTag(box.Id) };

        // Primary key — full data
        await cache.SetAsync(
            BoxIdKey(box.Id),
            box,
            tags: tags,
            cancellationToken: cancellationToken);

        // Pointer: ExtId → int Id
        await cache.SetAsync(
            BoxExtIdKey(box.ExternalId),
            box.Id,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<BoxContext?> BuildContext(
        BoxCached box,
        CancellationToken cancellationToken)
    {
        var workspaceContext = await workspaceCache.TryGetWorkspace(
            workspaceId: box.WorkspaceId,
            cancellationToken: cancellationToken);

        if (workspaceContext is null)
            return null;

        return new BoxContext(
            Id: box.Id,
            ExternalId: box.ExternalId,
            Name: box.Name,
            IsEnabled: box.IsEnabled,
            IsBeingDeleted: box.IsBeingDeleted,
            Workspace: workspaceContext,
            Folder: box.FolderId is null
                ? null
                : new FolderContext(
                    Id: box.FolderId.Value,
                    ExternalId: box.FolderExternalId!.Value));
    }

    public ValueTask InvalidateEntry(
        int boxId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            BoxTag(boxId),
            cancellationToken);
    }

    public async ValueTask InvalidateEntry(
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        var boxId = await cache.GetOrCreateAsync<int?>(
            key: BoxExtIdKey(boxExternalId),
            factory: _ => ValueTask.FromResult<int?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);

        if (boxId is not null)
        {
            await InvalidateEntry(boxId.Value, cancellationToken);
        }
        else
        {
            await cache.RemoveAsync(
                BoxExtIdKey(boxExternalId),
                cancellationToken);
        }
    }

    public async ValueTask InvalidateEntries(
        IEnumerable<int> boxIds,
        CancellationToken cancellationToken)
    {
        foreach (var boxId in boxIds)
        {
            await InvalidateEntry(boxId, cancellationToken);
        }
    }

    private BoxCached? LoadBox(BoxLookup lookup)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: $"""
                     SELECT
                         bo_id,
                         bo_external_id,
                         bo_workspace_id,
                         bo_name,
                         bo_is_enabled,
                         bo_is_being_deleted,
                         bo_folder_id,
                         fo_external_id
                     FROM bo_boxes
                     LEFT JOIN fo_folders
                         ON fo_id = bo_folder_id
                     WHERE {lookup.WhereClause}
                     LIMIT 1
                     """,
                readRowFunc: reader => new BoxCached(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<BoxExtId>(1),
                    WorkspaceId: reader.GetInt32(2),
                    Name: reader.GetString(3),
                    IsEnabled: reader.GetBoolean(4),
                    IsBeingDeleted: reader.GetBoolean(5),
                    FolderId: reader.GetInt32OrNull(6),
                    FolderExternalId: reader.GetExtIdOrNull<FolderExtId>(7)))
            .WithParameter(lookup.ParamName, lookup.ParamValue)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private readonly record struct BoxLookup(
        string WhereClause,
        string ParamName,
        object ParamValue)
    {
        public static BoxLookup ById(int id) =>
            new("bo_id = $boxId", "$boxId", id);

        public static BoxLookup ByExternalId(BoxExtId extId) =>
            new("bo_external_id = $boxExternalId", "$boxExternalId", extId.Value);
    }

    [ImmutableObject(true)]
    public sealed record BoxCached(
        int Id,
        BoxExtId ExternalId,
        int WorkspaceId,
        string Name,
        bool IsEnabled,
        bool IsBeingDeleted,
        int? FolderId,
        FolderExtId? FolderExternalId);
}