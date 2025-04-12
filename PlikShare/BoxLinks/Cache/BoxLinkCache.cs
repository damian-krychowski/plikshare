using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.BoxLinks.Cache;

public class BoxLinkCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    BoxCache boxCache)
{
    private readonly ConcurrentDictionary<int, BoxLinkExtId> _idExtIdMap = new();
    private readonly ConcurrentDictionary<string, BoxLinkExtId> _accessCodeExtIdMap = new();

    private static string BoxLinkKey(BoxLinkExtId externalId) => $"box-link:external-id:{externalId}";

    public async ValueTask<BoxLinkContext?> TryGetBoxLink(
        int boxLinkId,
        CancellationToken cancellationToken)
    {
        if (_idExtIdMap.TryGetValue(boxLinkId, out var externalId))
            return await TryGetBoxLink(externalId, cancellationToken);

        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, boxLink) = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         bl_id,
                         bl_external_id,
                         bl_box_id,
                         bl_name,
                         bl_is_enabled,
                         bl_allow_download,
                         bl_allow_upload,
                         bl_allow_list,
                         bl_allow_delete_file,
                         bl_allow_rename_file,
                         bl_allow_move_items,
                         bl_allow_create_folder,
                         bl_allow_rename_folder,
                         bl_allow_delete_folder,
                         bl_access_code,
                         bl_widget_origins
                     FROM bl_box_links
                     WHERE bl_id = $id
                     LIMIT 1
                     """,
                readRowFunc: reader => new BoxLinkCached(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<BoxLinkExtId>(1),
                    BoxId: reader.GetInt32(2),
                    Name: reader.GetString(3),
                    IsEnabled: reader.GetBoolean(4),
                    Permission: new BoxPermissions(
                        AllowDownload: reader.GetBoolean(5),
                        AllowUpload: reader.GetBoolean(6),
                        AllowList: reader.GetBoolean(7),
                        AllowDeleteFile: reader.GetBoolean(8),
                        AllowRenameFile: reader.GetBoolean(9),
                        AllowMoveItems: reader.GetBoolean(10),
                        AllowCreateFolder: reader.GetBoolean(11),
                        AllowRenameFolder: reader.GetBoolean(12),
                        AllowDeleteFolder: reader.GetBoolean(13)),
                    AccessCode: reader.GetString(14),
                    WidgetOrigins: reader.GetFromJsonOrNull<List<string>>(15)))
            .WithParameter("$id", boxLinkId)
            .Execute();

        if (isEmpty)
            return null;

        UpdateIdMap(boxLink);

        await cache.SetAsync(
            key: BoxLinkKey(boxLink.ExternalId),
            value: boxLink,
            cancellationToken: cancellationToken);

        return await PrepareBoxLinkContext(
            boxLink,
            cancellationToken);
    }

    public async ValueTask<BoxLinkContext?> TryGetBoxLink(
        string accessCode,
        CancellationToken cancellationToken)
    {
        if (_accessCodeExtIdMap.TryGetValue(accessCode, out var externalId))
            return await TryGetBoxLink(externalId, cancellationToken);
        
        using var connection = plikShareDb.OpenConnection();
        
        var (isEmpty, boxLink) = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         bl_id,
                         bl_external_id,
                         bl_box_id,
                         bl_name,
                         bl_is_enabled,
                         bl_allow_download,
                         bl_allow_upload,
                         bl_allow_list,
                         bl_allow_delete_file,
                         bl_allow_rename_file,
                         bl_allow_move_items,
                         bl_allow_create_folder,
                         bl_allow_rename_folder,
                         bl_allow_delete_folder,
                         bl_access_code,
                         bl_widget_origins
                     FROM bl_box_links
                     WHERE bl_access_code = $accessCode
                     LIMIT 1
                     """,
                readRowFunc: reader => new BoxLinkCached(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<BoxLinkExtId>(1),
                    BoxId: reader.GetInt32(2),
                    Name: reader.GetString(3),
                    IsEnabled: reader.GetBoolean(4),
                    Permission: new BoxPermissions(
                        AllowDownload: reader.GetBoolean(5),
                        AllowUpload: reader.GetBoolean(6),
                        AllowList: reader.GetBoolean(7),
                        AllowDeleteFile: reader.GetBoolean(8),
                        AllowRenameFile: reader.GetBoolean(9),
                        AllowMoveItems: reader.GetBoolean(10),
                        AllowCreateFolder: reader.GetBoolean(11),
                        AllowRenameFolder: reader.GetBoolean(12),
                        AllowDeleteFolder: reader.GetBoolean(13)),
                    AccessCode: reader.GetString(14),
                    WidgetOrigins: reader.GetFromJsonOrNull<List<string>>(15)))
            .WithParameter("$accessCode", accessCode)
            .Execute();

        if (isEmpty)
            return null;

        UpdateIdMap(boxLink);
        
        await cache.SetAsync(
            key: BoxLinkKey(boxLink.ExternalId),
            value: boxLink,
            cancellationToken: cancellationToken);

        return await PrepareBoxLinkContext(
            boxLink, 
            cancellationToken);
    }

    public async ValueTask<BoxLinkContext?> TryGetBoxLink(
        BoxLinkExtId externalId,
        CancellationToken cancellationToken)
    { 
        var boxLinkCached = await cache.GetOrCreateAsync(
            key: BoxLinkKey(externalId),
            factory: _ => ValueTask.FromResult(LoadBoxLink(externalId)), 
            cancellationToken: cancellationToken);

        if (boxLinkCached is null)
            return null;

        return await PrepareBoxLinkContext(
            boxLinkCached, 
            cancellationToken);
    }
    
    private async Task<BoxLinkContext?> PrepareBoxLinkContext(
        BoxLinkCached boxLinkCached,
        CancellationToken cancellationToken)
    {
        var boxContext = await boxCache.TryGetBox(
            boxId: boxLinkCached.BoxId,
            cancellationToken: cancellationToken);

        if (boxContext is null)
            return null;

        return new BoxLinkContext(
            Id: boxLinkCached.Id,
            ExternalId: boxLinkCached.ExternalId,
            Name: boxLinkCached.Name,
            IsEnabled: boxLinkCached.IsEnabled,
            Permissions: boxLinkCached.Permission,
            Box: boxContext,
            WidgetOrigins: boxLinkCached.WidgetOrigins);
    }

    public ValueTask InvalidateEntry(
        BoxLinkExtId externalId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            BoxLinkKey(externalId),
            cancellationToken);
    }
    
    private BoxLinkCached? LoadBoxLink(
        BoxLinkExtId externalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, boxLink) = connection
            .OneRowCmd(
                sql: """
                     SELECT
                     	bl_id,
                     	bl_external_id,
                     	bl_box_id,
                     	bl_name,
                     	bl_is_enabled,
                     	bl_allow_download,
                     	bl_allow_upload,
                     	bl_allow_list,
                     	bl_allow_delete_file,
                     	bl_allow_rename_file,
                     	bl_allow_move_items,
                     	bl_allow_create_folder,
                     	bl_allow_rename_folder,
                     	bl_allow_delete_folder,
                     	bl_access_code,
                        bl_widget_origins
                    FROM bl_box_links
                    WHERE bl_external_id = $externalId
                    LIMIT 1
                    """,
                readRowFunc: reader => new BoxLinkCached(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<BoxLinkExtId>(1),
                    BoxId: reader.GetInt32(2),
                    Name: reader.GetString(3),
                    IsEnabled: reader.GetBoolean(4),
                    Permission: new BoxPermissions(
                        AllowDownload: reader.GetBoolean(5),
                        AllowUpload: reader.GetBoolean(6),
                        AllowList: reader.GetBoolean(7),
                        AllowDeleteFile: reader.GetBoolean(8),
                        AllowRenameFile: reader.GetBoolean(9),
                        AllowMoveItems: reader.GetBoolean(10),
                        AllowCreateFolder: reader.GetBoolean(11),
                        AllowRenameFolder: reader.GetBoolean(12),
                        AllowDeleteFolder: reader.GetBoolean(13)),
                    AccessCode: reader.GetString(14),
                    WidgetOrigins: reader.GetFromJsonOrNull<List<string>>(15)))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        if (isEmpty)
            return null;

        UpdateIdMap(boxLink);

        return boxLink;
    }
    
    private void UpdateIdMap(
        BoxLinkCached boxLink)
    {
        _idExtIdMap.AddOrUpdate(
            key: boxLink.Id,
            addValueFactory: _ => boxLink.ExternalId,
            updateValueFactory: (_, _) => boxLink.ExternalId);

        _accessCodeExtIdMap.AddOrUpdate(
            key: boxLink.AccessCode,
            addValueFactory: _ => boxLink.ExternalId,
            updateValueFactory: (_, _) => boxLink.ExternalId);
    }

    [ImmutableObject(true)]
    public sealed record BoxLinkCached(
        int Id,
        BoxLinkExtId ExternalId,
        int BoxId,
        string Name,
        bool IsEnabled,
        BoxPermissions Permission,
        string AccessCode,
        List<string>? WidgetOrigins);
}