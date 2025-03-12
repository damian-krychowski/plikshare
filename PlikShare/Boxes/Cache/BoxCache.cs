using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<int, BoxExtId> _idExtIdMap = new();

    private static string BoxKey(BoxExtId externalId) => $"box:external-id:{externalId}";
    
    public async ValueTask<BoxContext?> TryGetBox(
        BoxExtId boxExternalId,
        CancellationToken cancellation)
    {
        var boxCached = await cache.GetOrCreateAsync(
            key: BoxKey(boxExternalId),
            factory: _ => ValueTask.FromResult(LoadBox(boxExternalId)),
            cancellationToken: cancellation);

        if (boxCached is null)
            return null;
        
        var workspaceContext = await workspaceCache.TryGetWorkspace(
            workspaceId: boxCached.WorkspaceId,
            cancellationToken: cancellation);

        if (workspaceContext is null)
            return null;

        return new BoxContext(
            Id: boxCached.Id,
            ExternalId: boxCached.ExternalId,
            Name: boxCached.Name,
            IsEnabled: boxCached.IsEnabled,
            IsBeingDeleted: boxCached.IsBeingDeleted,
            Workspace: workspaceContext,
            Folder: boxCached.FolderId is null
                ? null
                : new FolderContext(
                    Id: boxCached.FolderId.Value,
                    ExternalId: boxCached.FolderExternalId!.Value));
    }

    public async ValueTask<BoxContext?> TryGetBox(
        int boxId,
        CancellationToken cancellationToken)
    {
        if (_idExtIdMap.TryGetValue(boxId, out var externalId))
            return await TryGetBox(externalId, cancellationToken);

        using var connection = plikShareDb.OpenConnection();
        
        var (isEmpty, box) = connection
            .OneRowCmd(
                sql: """
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
                     WHERE
                         bo_id = $boxId
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
                    FolderExternalId: reader.GetExtIdOrNull<FolderExtId>(7))
            )
            .WithParameter("$boxId", boxId)
            .Execute();

        if (isEmpty)
            return null;
        
        UpdateIdMap(
            boxId: box.Id,
            boxExternalId: box.ExternalId);

        await cache.SetAsync(
            key: BoxKey(box.ExternalId),
            value: box,
            cancellationToken: cancellationToken);
        
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
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            BoxKey(boxExternalId), 
            cancellationToken);
    }
    
    public async ValueTask InvalidateEntry(
        int boxId,
        CancellationToken cancellationToken)
    {
        if (_idExtIdMap.Remove(boxId, out var externalId))
        {
            await cache.RemoveAsync(
                BoxKey(externalId),
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
    
    private BoxCached? LoadBox(
        BoxExtId externalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, box) = connection
            .OneRowCmd(
                sql: """
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
                     WHERE
                         bo_external_id = $boxExternalId
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
                    FolderExternalId: reader.GetExtIdOrNull<FolderExtId>(7))
            )
            .WithParameter("$boxExternalId", externalId.Value)
            .Execute();

        if (isEmpty)
            return null;

        UpdateIdMap(
            boxId: box.Id,
            boxExternalId: box.ExternalId);

        return box;
    }
    
    private void UpdateIdMap(
        int boxId,
        BoxExtId boxExternalId)
    {
        _idExtIdMap.AddOrUpdate(
            key: boxId,
            addValueFactory: _ => boxExternalId,
            updateValueFactory: (_, _) => boxExternalId);
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