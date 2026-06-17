using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.Tools;

/// <summary>
/// Reads the per-workspace tool override an agent has for a tool, so the invocation-time gate can
/// cascade it on top of the agent's global config (and the catalog default). A missing row means
/// "no override — inherit"; each dimension is independently nullable.
/// </summary>
public class AgentWorkspaceToolOverrideReader(PlikShareDb plikShareDb)
{
    public AgentToolScopeOverride? TryGet(
        int agentId,
        int workspaceId,
        string toolName)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT atwo_is_enabled, atwo_requires_approval
                     FROM atwo_agent_tool_workspace_overrides
                     WHERE atwo_agent_id = $agentId
                         AND atwo_workspace_id = $workspaceId
                         AND atwo_tool_name = $toolName
                     LIMIT 1
                     """,
                readRowFunc: reader => new AgentToolScopeOverride(
                    IsEnabled: ToNullableBool(reader.GetInt32OrNull(0)),
                    RequiresApproval: ToNullableBool(reader.GetInt32OrNull(1))))
            .WithParameter("$agentId", agentId)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$toolName", toolName)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static bool? ToNullableBool(int? value) =>
        value is null ? null : value != 0;
}
