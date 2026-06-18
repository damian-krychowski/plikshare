using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Agents.Cache;
using PlikShare.Agents.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;
using System.ComponentModel;

namespace PlikShare.Workspaces.Cache;

public class WorkspaceAgentMembershipCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    WorkspaceCache workspaceCache,
    AgentCache agentCache)
{
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string MembershipKey(int workspaceId, int agentId)
        => $"workspace-agent-membership:{workspaceId}:{agentId}";

    private static string WorkspaceAgentMembershipsTag(int workspaceId)
        => $"workspace-agent-memberships-{workspaceId}";

    private static string AgentMembershipsTag(int agentId)
        => $"agent-memberships-{agentId}";

    public async ValueTask<WorkspaceAgentMembershipContext?> TryGetWorkspaceAgentMembership(
        int workspaceId,
        int agentId,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId,
            cancellationToken);

        var agent = await agentCache.TryGetAgent(
            agentId,
            cancellationToken);

        return await TryGetWorkspaceAgentMembership(
            workspace: workspace,
            agent: agent,
            cancellationToken: cancellationToken);
    }

    public async ValueTask<WorkspaceAgentMembershipContext?> TryGetWorkspaceAgentMembership(
        WorkspaceExtId workspaceExternalId,
        AgentExtId agentExternalId,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceExternalId,
            cancellationToken);

        var agent = await agentCache.TryGetAgent(
            agentExternalId,
            cancellationToken);

        return await TryGetWorkspaceAgentMembership(
            workspace: workspace,
            agent: agent,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<WorkspaceAgentMembershipContext?> TryGetWorkspaceAgentMembership(
        WorkspaceContext? workspace,
        AgentContext? agent,
        CancellationToken cancellationToken)
    {
        if (workspace is null || agent is null)
            return null;

        if (workspace.OwnerAgent?.Id == agent.Id)
        {
            return new WorkspaceAgentMembershipContext(
                Agent: agent,
                Workspace: workspace,
                IsSharedWithAgent: false);
        }

        var cachedMembership = await ProbeMembershipCache(
            workspace.Id,
            agent.Id,
            cancellationToken);

        if (cachedMembership is null)
        {
            var loaded = LoadMembership(
                workspace.Id,
                agent.Id);

            if (loaded is null)
                return null;

            await StoreMembership(
                workspace.Id,
                agent.Id,
                loaded,
                cancellationToken);

            cachedMembership = loaded;
        }

        return new WorkspaceAgentMembershipContext(
            Agent: agent,
            Workspace: workspace,
            IsSharedWithAgent: true);
    }

    private async ValueTask<WorkspaceAgentMembershipCached?> ProbeMembershipCache(
        int workspaceId,
        int agentId,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync<WorkspaceAgentMembershipCached?>(
            key: MembershipKey(workspaceId, agentId),
            factory: _ => ValueTask.FromResult<WorkspaceAgentMembershipCached?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private ValueTask StoreMembership(
        int workspaceId,
        int agentId,
        WorkspaceAgentMembershipCached membership,
        CancellationToken cancellationToken)
    {
        var tags = new[]
        {
            WorkspaceAgentMembershipsTag(workspaceId),
            AgentMembershipsTag(agentId)
        };

        return cache.SetAsync(
            MembershipKey(workspaceId, agentId),
            membership,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    public ValueTask InvalidateEntry(
        int workspaceId,
        int agentId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            MembershipKey(workspaceId, agentId),
            cancellationToken);
    }

    public ValueTask InvalidateEntry(
        WorkspaceAgentMembershipContext workspaceAgentMembership,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            MembershipKey(
                workspaceAgentMembership.Workspace.Id,
                workspaceAgentMembership.Agent.Id),
            cancellationToken);
    }

    public ValueTask InvalidateAllForWorkspace(
        int workspaceId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            WorkspaceAgentMembershipsTag(workspaceId),
            cancellationToken);
    }

    public ValueTask InvalidateAllForAgent(
        int agentId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            AgentMembershipsTag(agentId),
            cancellationToken);
    }

    private WorkspaceAgentMembershipCached? LoadMembership(
        int workspaceId,
        int agentId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         wa_created_at
                     FROM wa_workspace_agents
                     WHERE
                         wa_workspace_id = $workspaceId
                         AND wa_agent_id = $agentId
                     LIMIT 1
                     """,
                readRowFunc: reader => new WorkspaceAgentMembershipCached(
                    CreatedAt: reader.GetFieldValue<DateTimeOffset>(0)))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$agentId", agentId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    [ImmutableObject(true)]
    public sealed record WorkspaceAgentMembershipCached(
        DateTimeOffset CreatedAt);
}
