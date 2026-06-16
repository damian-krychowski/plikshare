using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Agents.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.QuickShares.Cache;

public class QuickShareCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    WorkspaceCache workspaceCache)
{
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string ExtIdKey(QuickShareExtId extId) => $"quickshare:extid:{extId.Value}";
    private static string IdKey(int id) => $"quickshare:id:{id}";
    private static string SlugKey(string slug) => $"quickshare:slug:{slug}";
    private static string Tag(int id) => $"quickshare-{id}";

    public static byte[] HashSecret(string secret)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public async ValueTask<QuickShareContext?> TryGetQuickShare(
        QuickShareExtId externalId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeCache(
            ExtIdKey(externalId),
            cancellationToken);

        if (cached is not null)
            return await BuildContext(cached, cancellationToken);

        var quickShare = LoadQuickShare(
            QuickShareLookup.ByExternalId(externalId));

        if (quickShare is null)
            return null;

        await StoreInAllKeys(quickShare, cancellationToken);
        return await BuildContext(quickShare, cancellationToken);
    }

    public async ValueTask<QuickShareContext?> TryGetQuickShare(
        int quickShareId,
        CancellationToken cancellationToken)
    {
        var extId = await cache.GetOrCreateAsync(
            key: IdKey(quickShareId),
            factory: _ => ValueTask.FromResult<QuickShareExtId?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);

        if (extId is not null)
            return await TryGetQuickShare(extId.Value, cancellationToken);

        var quickShare = LoadQuickShare(
            QuickShareLookup.ById(quickShareId));

        if (quickShare is null)
            return null;

        await StoreInAllKeys(quickShare, cancellationToken);
        return await BuildContext(quickShare, cancellationToken);
    }

    public async ValueTask<QuickShareContext?> TryGetQuickShareBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var extId = await cache.GetOrCreateAsync(
            key: SlugKey(slug),
            factory: _ => ValueTask.FromResult<QuickShareExtId?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);

        if (extId is not null)
            return await TryGetQuickShare(extId.Value, cancellationToken);

        var quickShare = LoadQuickShare(
            QuickShareLookup.BySlug(slug));

        if (quickShare is null)
            return null;

        await StoreInAllKeys(quickShare, cancellationToken);
        return await BuildContext(quickShare, cancellationToken);
    }

    private ValueTask<QuickShareCached?> ProbeCache(
        string key,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync<QuickShareCached?>(
            key: key,
            factory: _ => ValueTask.FromResult<QuickShareCached?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask StoreInAllKeys(
        QuickShareCached quickShare,
        CancellationToken cancellationToken)
    {
        var tags = new[] { Tag(quickShare.Id) };

        await cache.SetAsync(
            ExtIdKey(quickShare.ExternalId),
            quickShare,
            tags: tags,
            cancellationToken: cancellationToken);

        await cache.SetAsync(
            IdKey(quickShare.Id),
            quickShare.ExternalId,
            tags: tags,
            cancellationToken: cancellationToken);

        await cache.SetAsync(
            SlugKey(quickShare.Slug),
            quickShare.ExternalId,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<QuickShareContext?> BuildContext(
        QuickShareCached quickShare,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId: quickShare.WorkspaceId,
            cancellationToken: cancellationToken);

        if (workspace is null)
            return null;

        return new QuickShareContext(
            Id: quickShare.Id,
            ExternalId: quickShare.ExternalId,
            Name: quickShare.Name,
            Workspace: workspace,
            CreatorExternalId: quickShare.CreatorExternalId,
            CreatorAgentExternalId: quickShare.CreatorAgentExternalId,
            Slug: quickShare.Slug,
            SecretHash: quickShare.SecretHash,
            CreatedAt: quickShare.CreatedAt,
            ExpiresAt: quickShare.ExpiresAt,
            PasswordHash: quickShare.PasswordHash,
            PasswordSalt: quickShare.PasswordSalt,
            MaxDownloads: quickShare.MaxDownloads,
            DownloadsCount: quickShare.DownloadsCount,
            Mode: quickShare.Mode,
            AllowIndividualFileDownload: quickShare.AllowIndividualFileDownload,
            LastAccessedAt: quickShare.LastAccessedAt);
    }

    public ValueTask InvalidateEntry(
        int quickShareId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(Tag(quickShareId), cancellationToken);
    }

    public async ValueTask InvalidateEntry(
        QuickShareExtId externalId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeCache(ExtIdKey(externalId), cancellationToken);

        if (cached is not null)
        {
            await InvalidateEntry(cached.Id, cancellationToken);
        }
        else
        {
            await cache.RemoveAsync(ExtIdKey(externalId), cancellationToken);
        }
    }

    private QuickShareCached? LoadQuickShare(QuickShareLookup lookup)
    {
        using var connection = plikShareDb.OpenConnection();

        var cmd = connection.OneRowCmd(
            sql: $"""
                 SELECT
                     qsh_id,
                     qsh_external_id,
                     qsh_workspace_id,
                     u_external_id,
                     qsh_slug,
                     qsh_secret_hash,
                     qsh_name,
                     qsh_created_at,
                     qsh_expires_at,
                     qsh_password_hash,
                     qsh_password_salt,
                     qsh_max_downloads,
                     qsh_downloads_count,
                     qsh_mode,
                     qsh_allow_individual_file_download,
                     qsh_last_accessed_at,
                     a_external_id
                 FROM qsh_quick_shares
                 INNER JOIN u_users ON u_id = qsh_creator_id
                 LEFT JOIN a_agents ON a_id = qsh_creator_agent_id
                 WHERE {lookup.WhereClause}
                 LIMIT 1
                 """,
            readRowFunc: reader => new QuickShareCached(
                Id: reader.GetInt32(0),
                ExternalId: reader.GetExtId<QuickShareExtId>(1),
                WorkspaceId: reader.GetInt32(2),
                CreatorExternalId: reader.GetExtId<UserExtId>(3),
                Slug: reader.GetString(4),
                SecretHash: reader.GetFieldValueOrNull<byte[]>(5),
                Name: reader.GetString(6),
                CreatedAt: reader.GetFieldValue<DateTimeOffset>(7),
                ExpiresAt: reader.GetDateTimeOffsetOrNull(8),
                PasswordHash: reader.GetStringOrNull(9),
                PasswordSalt: reader.GetFieldValueOrNull<byte[]>(10),
                MaxDownloads: reader.GetInt32OrNull(11),
                DownloadsCount: reader.GetInt32(12),
                Mode: reader.GetEnum<QuickShareMode>(13),
                AllowIndividualFileDownload: reader.GetBoolean(14),
                LastAccessedAt: reader.GetDateTimeOffsetOrNull(15),
                CreatorAgentExternalId: reader.IsDBNull(16)
                    ? null
                    : reader.GetExtId<AgentExtId>(16)));

        foreach (var (name, value) in lookup.Parameters)
            cmd = cmd.WithParameter(name, value);

        var result = cmd.Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private readonly record struct QuickShareLookup(
        string WhereClause,
        (string Name, object Value)[] Parameters)
    {
        public static QuickShareLookup ByExternalId(QuickShareExtId extId) =>
            new("qsh_external_id = $externalId",
                [("$externalId", extId.Value)]);

        public static QuickShareLookup ById(int id) =>
            new("qsh_id = $id",
                [("$id", id)]);

        public static QuickShareLookup BySlug(string slug) =>
            new("qsh_slug = $slug",
                [("$slug", slug)]);
    }

    [ImmutableObject(true)]
    public sealed record QuickShareCached(
        int Id,
        QuickShareExtId ExternalId,
        int WorkspaceId,
        UserExtId CreatorExternalId,
        string Slug,
        byte[]? SecretHash,
        string Name,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        string? PasswordHash,
        byte[]? PasswordSalt,
        int? MaxDownloads,
        int DownloadsCount,
        QuickShareMode Mode,
        bool AllowIndividualFileDownload,
        DateTimeOffset? LastAccessedAt,
        AgentExtId? CreatorAgentExternalId);
}
