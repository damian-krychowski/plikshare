using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Agents.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Users.StorageAccess;

namespace PlikShare.Agents.Cache;

public class AgentCache(
    PlikShareDb plikShareDb,
    HybridCache cache)
{
    // Probe options: read from cache without writing anything back when the factory returns null.
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string AgentIdKey(int agentId) => $"agent:id:{agentId}";
    private static string AgentExtIdKey(AgentExtId extId) => $"agent:extid:{extId.Value}";
    private static string AgentTag(int agentId) => $"agent-{agentId}";

    public async ValueTask<AgentContext?> TryGetAgent(
        int agentId,
        CancellationToken cancellationToken)
    {
        return await GetOrLoadAsync(
            primaryKey: AgentIdKey(agentId),
            loader: () => GetAgent(AgentLookup.ById(agentId)),
            cancellationToken: cancellationToken);
    }

    public async ValueTask<AgentContext?> TryGetAgent(
        AgentExtId agentExternalId,
        CancellationToken cancellationToken)
    {
        return await GetOrLoadAsync(
            primaryKey: AgentExtIdKey(agentExternalId),
            loader: () => GetAgent(AgentLookup.ByExternalId(agentExternalId)),
            cancellationToken: cancellationToken);
    }

    public async ValueTask<AgentContext> GetOrThrow(
        AgentExtId agentExternalId,
        CancellationToken cancellationToken)
    {
        var agent = await TryGetAgent(agentExternalId, cancellationToken);

        return agent ?? throw new InvalidOperationException(
            $"Agent with external id '{agentExternalId.Value}' was not found.");
    }

    private async ValueTask<AgentContext?> GetOrLoadAsync(
        string primaryKey,
        Func<AgentContext?> loader,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeCache(
            primaryKey,
            cancellationToken);

        if (cached is not null)
            return cached;

        var agent = loader();

        if (agent is null)
            return null;

        await StoreInAllKeys(
            agent,
            cancellationToken);

        return agent;
    }

    // Reads the cache without polluting it with a null entry on miss.
    private ValueTask<AgentContext?> ProbeCache(
        string key,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync<AgentContext?>(
            key: key,
            factory: _ => ValueTask.FromResult<AgentContext?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask StoreInAllKeys(
        AgentContext agent,
        CancellationToken cancellationToken)
    {
        var tags = new[] { AgentTag(agent.Id) };

        await cache.SetAsync(
            AgentIdKey(agent.Id),
            agent,
            tags: tags,
            cancellationToken: cancellationToken);

        await cache.SetAsync(
            AgentExtIdKey(agent.ExternalId),
            agent,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    public ValueTask InvalidateEntry(
        int agentId,
        CancellationToken cancellationToken)
    {
        // A single tag removal clears every key associated with the agent.
        return cache.RemoveByTagAsync(
            AgentTag(agentId),
            cancellationToken);
    }

    public async ValueTask InvalidateEntry(
        AgentExtId agentExternalId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeCache(
            AgentExtIdKey(agentExternalId),
            cancellationToken);

        if (cached is not null)
        {
            await InvalidateEntry(
                cached.Id,
                cancellationToken);
        }
        else
        {
            // Fallback: at least drop the pointer key if the agent is gone from the DB.
            await cache.RemoveAsync(
                AgentExtIdKey(agentExternalId),
                cancellationToken);
        }
    }

    private AgentContext? GetAgent(AgentLookup lookup)
    {
        using var connection = plikShareDb.OpenConnection();

        var row = ReadAgentRow(connection, lookup);

        if (row is null)
            return null;

        var storageAccessIds = row.StorageAccessMode == UserStorageAccessMode.All
            ? []
            : ReadStorageAccessIds(connection, row.Id);

        var toolConfigs = ReadToolConfigs(connection, row.Id);

        return new AgentContext
        {
            Id = row.Id,
            ExternalId = row.ExternalId,
            Name = row.Name,
            IsEnabled = row.IsEnabled,
            Owner = new AgentOwnerContext
            {
                Id = row.OwnerId,
                ExternalId = row.OwnerExternalId
            },
            MaxWorkspaceNumber = row.MaxWorkspaceNumber,
            DefaultMaxWorkspaceSizeInBytes = row.DefaultMaxWorkspaceSizeInBytes,
            DefaultMaxWorkspaceTeamMembers = row.DefaultMaxWorkspaceTeamMembers,
            StorageAccess = new UserStorageAccess
            {
                Mode = row.StorageAccessMode,
                StorageIds = storageAccessIds
            },
            ToolConfigs = toolConfigs
        };
    }

    private static IReadOnlyDictionary<string, AgentToolConfigEntry> ReadToolConfigs(
        SqliteConnection connection,
        int agentId)
    {
        var rows = connection
            .Cmd(
                sql: """
                     SELECT
                         atc_tool_name,
                         atc_is_enabled,
                         atc_requires_approval
                     FROM atc_agent_tool_configs
                     WHERE atc_agent_id = $agentId
                     """,
                readRowFunc: reader => new
                {
                    ToolName = reader.GetString(0),
                    IsEnabled = reader.GetBoolean(1),
                    RequiresApproval = reader.GetBoolean(2)
                })
            .WithParameter("$agentId", agentId)
            .Execute();

        return rows.ToDictionary(
            row => row.ToolName,
            row => new AgentToolConfigEntry
            {
                IsEnabled = row.IsEnabled,
                RequiresApproval = row.RequiresApproval
            });
    }

    private static AgentRow? ReadAgentRow(SqliteConnection connection, AgentLookup lookup)
    {
        var result = connection
            .OneRowCmd(
                sql: $"""
                     SELECT
                         a_id,
                         a_external_id,
                         a_name,
                         a_is_enabled,
                         a_max_workspace_number,
                         a_default_max_workspace_size_in_bytes,
                         a_default_max_workspace_team_members,
                         a_storage_access_mode,
                         a_owner_user_id,
                         u_external_id
                     FROM a_agents
                     INNER JOIN u_users ON u_id = a_owner_user_id
                     WHERE {lookup.WhereClause}
                     LIMIT 1
                     """,
                readRowFunc: reader => new AgentRow(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<AgentExtId>(1),
                    Name: reader.GetString(2),
                    IsEnabled: reader.GetBoolean(3),
                    MaxWorkspaceNumber: reader.GetInt32OrNull(4),
                    DefaultMaxWorkspaceSizeInBytes: reader.GetInt64OrNull(5),
                    DefaultMaxWorkspaceTeamMembers: reader.GetInt32OrNull(6),
                    StorageAccessMode: reader.GetEnum<UserStorageAccessMode>(7),
                    OwnerId: reader.GetInt32(8),
                    OwnerExternalId: reader.GetExtId<UserExtId>(9)))
            .WithParameter(lookup.ParamName, lookup.ParamValue)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static int[] ReadStorageAccessIds(SqliteConnection connection, int agentId)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT asa_storage_id
                     FROM asa_agent_storage_access
                     WHERE asa_agent_id = $agentId
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$agentId", agentId)
            .Execute()
            .ToArray();
    }

    private sealed record AgentRow(
        int Id,
        AgentExtId ExternalId,
        string Name,
        bool IsEnabled,
        int? MaxWorkspaceNumber,
        long? DefaultMaxWorkspaceSizeInBytes,
        int? DefaultMaxWorkspaceTeamMembers,
        UserStorageAccessMode StorageAccessMode,
        int OwnerId,
        UserExtId OwnerExternalId);

    private readonly record struct AgentLookup(
        string WhereClause,
        string ParamName,
        object ParamValue)
    {
        public static AgentLookup ById(int agentId) =>
            new("a_id = $agentId", "$agentId", agentId);

        public static AgentLookup ByExternalId(AgentExtId extId) =>
            new("a_external_id = $agentExternalId", "$agentExternalId", extId.Value);
    }
}
