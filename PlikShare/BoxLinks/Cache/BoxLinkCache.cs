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
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string BoxLinkExtIdKey(BoxLinkExtId extId) => $"boxlink:extid:{extId.Value}";
    private static string BoxLinkIdKey(int id) => $"boxlink:id:{id}";
    private static string BoxLinkAccessCodeKey(string accessCode) => $"boxlink:accesscode:{accessCode}";
    private static string BoxLinkTag(int id) => $"boxlink-{id}";

    public async ValueTask<BoxLinkContext?> TryGetBoxLink(
        BoxLinkExtId externalId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeBoxLinkCache(
            BoxLinkExtIdKey(externalId),
            cancellationToken);

        if (cached is not null)
            return await BuildContext(
                cached, 
                cancellationToken);

        var boxLink = LoadBoxLink(
            BoxLinkLookup.ByExternalId(externalId));

        if (boxLink is null)
            return null;

        await StoreInAllKeys(
            boxLink, 
            cancellationToken);

        return await BuildContext(
            boxLink, 
            cancellationToken);
    }

    public async ValueTask<BoxLinkContext?> TryGetBoxLink(
        int boxLinkId,
        CancellationToken cancellationToken)
    {
        var extId = await cache.GetOrCreateAsync(
            key: BoxLinkIdKey(boxLinkId),
            factory: _ => ValueTask.FromResult<BoxLinkExtId?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);

        if (extId is not null)
            return await TryGetBoxLink(
                extId.Value, 
                cancellationToken);

        var boxLink = LoadBoxLink(
            BoxLinkLookup.ById(boxLinkId));

        if (boxLink is null)
            return null;

        await StoreInAllKeys(
            boxLink, 
            cancellationToken);

        return await BuildContext(
            boxLink, 
            cancellationToken);
    }

    public async ValueTask<BoxLinkContext?> TryGetBoxLink(
        string accessCode,
        CancellationToken cancellationToken)
    {
        var extId = await cache.GetOrCreateAsync(
            key: BoxLinkAccessCodeKey(accessCode),
            factory: _ => ValueTask.FromResult<BoxLinkExtId?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);

        if (extId is not null)
            return await TryGetBoxLink(
                extId.Value, 
                cancellationToken);

        var boxLink = LoadBoxLink(
            BoxLinkLookup.ByAccessCode(accessCode));

        if (boxLink is null)
            return null;

        await StoreInAllKeys(
            boxLink, 
            cancellationToken);

        return await BuildContext(
            boxLink, 
            cancellationToken);
    }

    private ValueTask<BoxLinkCached?> ProbeBoxLinkCache(
        string key,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync<BoxLinkCached?>(
            key: key,
            factory: _ => ValueTask.FromResult<BoxLinkCached?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask StoreInAllKeys(
        BoxLinkCached boxLink,
        CancellationToken cancellationToken)
    {
        var tags = new[] { BoxLinkTag(boxLink.Id) };

        // Primary key — full data
        await cache.SetAsync(
            BoxLinkExtIdKey(boxLink.ExternalId),
            boxLink,
            tags: tags,
            cancellationToken: cancellationToken);

        // Pointer: int Id → ExtId
        await cache.SetAsync(
            BoxLinkIdKey(boxLink.Id),
            boxLink.ExternalId,
            tags: tags,
            cancellationToken: cancellationToken);

        // Pointer: accessCode → ExtId
        await cache.SetAsync(
            BoxLinkAccessCodeKey(boxLink.AccessCode),
            boxLink.ExternalId,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<BoxLinkContext?> BuildContext(
        BoxLinkCached boxLink,
        CancellationToken cancellationToken)
    {
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
            Box: boxContext,
            WidgetOrigins: boxLink.WidgetOrigins);
    }

    public ValueTask InvalidateEntry(
        int boxLinkId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            BoxLinkTag(boxLinkId),
            cancellationToken);
    }

    public async ValueTask InvalidateEntry(
        BoxLinkExtId externalId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeBoxLinkCache(
            BoxLinkExtIdKey(externalId),
            cancellationToken);

        if (cached is not null)
        {
            await InvalidateEntry(cached.Id, cancellationToken);
        }
        else
        {
            await cache.RemoveAsync(
                BoxLinkExtIdKey(externalId),
                cancellationToken);
        }
    }

    private BoxLinkCached? LoadBoxLink(BoxLinkLookup lookup)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: $"""
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
                     WHERE {lookup.WhereClause}
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
            .WithParameter(lookup.ParamName, lookup.ParamValue)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private readonly record struct BoxLinkLookup(
        string WhereClause,
        string ParamName,
        object ParamValue)
    {
        public static BoxLinkLookup ByExternalId(BoxLinkExtId extId) =>
            new("bl_external_id = $externalId", "$externalId", extId.Value);

        public static BoxLinkLookup ById(int id) =>
            new("bl_id = $id", "$id", id);

        public static BoxLinkLookup ByAccessCode(string accessCode) =>
            new("bl_access_code = $accessCode", "$accessCode", accessCode);
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