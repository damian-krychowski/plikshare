using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using System.ComponentModel;

namespace PlikShare.Agents.BoxAccess;

/// <summary>
/// Caches whether an agent was granted direct access to a box (a <c>ba_box_agents</c> row), so the
/// box-access MCP tools do not hit the database on every call - mirroring
/// <see cref="PlikShare.Workspaces.Cache.WorkspaceAgentMembershipCache"/> for workspace memberships.
/// Only positive access is cached; the entry is invalidated when access is granted or revoked, when the
/// agent is deleted, or when the box is deleted.
/// </summary>
public class AgentBoxAccessCache(
    PlikShareDb plikShareDb,
    HybridCache cache)
{
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string AccessKey(int boxId, int agentId)
        => $"box-agent-access:{boxId}:{agentId}";

    private static string BoxAgentAccessesTag(int boxId)
        => $"box-agent-accesses-{boxId}";

    private static string AgentBoxAccessesTag(int agentId)
        => $"agent-box-accesses-{agentId}";

    public async ValueTask<bool> HasAccess(
        int agentId,
        int boxId,
        CancellationToken cancellationToken)
    {
        var cachedAccess = await ProbeAccessCache(
            boxId,
            agentId,
            cancellationToken);

        if (cachedAccess is not null)
            return true;

        var loaded = LoadAccess(
            boxId,
            agentId);

        if (loaded is null)
            return false;

        await StoreAccess(
            boxId,
            agentId,
            loaded,
            cancellationToken);

        return true;
    }

    private async ValueTask<AgentBoxAccessCached?> ProbeAccessCache(
        int boxId,
        int agentId,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync<AgentBoxAccessCached?>(
            key: AccessKey(boxId, agentId),
            factory: _ => ValueTask.FromResult<AgentBoxAccessCached?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private ValueTask StoreAccess(
        int boxId,
        int agentId,
        AgentBoxAccessCached access,
        CancellationToken cancellationToken)
    {
        var tags = new[]
        {
            BoxAgentAccessesTag(boxId),
            AgentBoxAccessesTag(agentId)
        };

        return cache.SetAsync(
            AccessKey(boxId, agentId),
            access,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    public ValueTask InvalidateEntry(
        int boxId,
        int agentId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            AccessKey(boxId, agentId),
            cancellationToken);
    }

    public ValueTask InvalidateAllForBox(
        int boxId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            BoxAgentAccessesTag(boxId),
            cancellationToken);
    }

    public ValueTask InvalidateAllForAgent(
        int agentId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            AgentBoxAccessesTag(agentId),
            cancellationToken);
    }

    private AgentBoxAccessCached? LoadAccess(
        int boxId,
        int agentId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT ba_created_at
                     FROM ba_box_agents
                     WHERE ba_box_id = $boxId
                         AND ba_agent_id = $agentId
                     LIMIT 1
                     """,
                readRowFunc: reader => new AgentBoxAccessCached(
                    CreatedAt: reader.GetFieldValue<DateTimeOffset>(0)))
            .WithParameter("$boxId", boxId)
            .WithParameter("$agentId", agentId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    [ImmutableObject(true)]
    public sealed record AgentBoxAccessCached(
        DateTimeOffset CreatedAt);
}
