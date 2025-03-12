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
    private readonly ConcurrentDictionary<BoxLinkExtId, string> _externalIdAccessCodeMap = new();

    private static string BoxLinkKey(string accessCode) => $"box-link:access-code:{accessCode}";

    public async ValueTask<BoxLinkContext?> TryGetBoxLink(
        BoxLinkExtId externalId,
        CancellationToken cancellationToken)
    {
        if (_externalIdAccessCodeMap.TryGetValue(externalId, out var accessCode))
            return await TryGetBoxLink(accessCode, cancellationToken);
        
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
                         bl_access_code
                     FROM bl_box_links
                     WHERE bl_external_id = $boxLinkExternalId
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
                    AccessCode: reader.GetString(14)))
            .WithParameter("$boxLinkExternalId", externalId.Value)
            .Execute();

        if (isEmpty)
            return null;

        UpdateIdMap(
            externalId: boxLink.ExternalId,
            accessCode: boxLink.AccessCode);
        
        await cache.SetAsync(
            key: BoxLinkKey(boxLink.AccessCode),
            value: boxLink,
            cancellationToken: cancellationToken);

        var boxContext = await boxCache.TryGetBox(
            boxId: boxLink.BoxId,
            cancellationToken: cancellationToken);

        if (boxContext is null)
            return null;

        return new BoxLinkContext(
            Id: boxLink.Id,
            ExternalId: boxLink.ExternalId,
            Name: boxLink.Name,
            IsEnabled: boxLink.IsEnabled,
            Permissions: boxLink.Permission,
            Box: boxContext);
    }
    
    public async ValueTask<BoxLinkContext?> TryGetBoxLink(
        string accessCode,
        CancellationToken cancellationToken)
    { 
        var boxLinkCached = await cache.GetOrCreateAsync(
            key: BoxLinkKey(accessCode),
            factory: _ => ValueTask.FromResult(LoadBoxLink(accessCode)), 
            cancellationToken: cancellationToken);

        if (boxLinkCached is null)
            return null;

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
            Box: boxContext);
    }
    
    public ValueTask InvalidateEntry(
        string accessCode,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            BoxLinkKey(accessCode), 
            cancellationToken);
    }
    
    public async ValueTask InvalidateEntry(
        BoxLinkExtId externalId,
        CancellationToken cancellationToken)
    {
        if (_externalIdAccessCodeMap.Remove(externalId, out var accessCode)) 
            await InvalidateEntry(accessCode, cancellationToken);
    }

    
    private BoxLinkCached? LoadBoxLink(
        string accessCode)
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
                     	bl_access_code
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
                    AccessCode: reader.GetString(14)))
            .WithParameter("$accessCode", accessCode)
            .Execute();

        if (isEmpty)
            return null;

        UpdateIdMap(
            externalId: boxLink.ExternalId,
            accessCode: boxLink.AccessCode);

        return boxLink;
    }
    
    private void UpdateIdMap(
        BoxLinkExtId externalId,
        string accessCode)
    {
        
        _externalIdAccessCodeMap.AddOrUpdate(
            key: externalId,
            addValueFactory: _ => accessCode,
            updateValueFactory: (_, _) => accessCode);
    }

    [ImmutableObject(true)]
    public sealed record BoxLinkCached(
        int Id,
        BoxLinkExtId ExternalId,
        int BoxId,
        string Name,
        bool IsEnabled,
        BoxPermissions Permission,
        string AccessCode);
}