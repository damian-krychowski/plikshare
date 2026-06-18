using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.Tools;

/// <summary>
/// Reads the per-box tool override an agent has for a tool, so the invocation-time gate can cascade it
/// on top of the per-workspace override, the agent's global config and the catalog default. A missing
/// row means "no override — inherit"; each dimension is independently nullable.
/// </summary>
public class AgentBoxToolOverrideReader(PlikShareDb plikShareDb)
{
    public AgentToolScopeOverride? TryGet(
        int agentId,
        int boxId,
        string toolName)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT atbo_is_enabled, atbo_requires_approval
                     FROM atbo_agent_tool_box_overrides
                     WHERE atbo_agent_id = $agentId
                         AND atbo_box_id = $boxId
                         AND atbo_tool_name = $toolName
                     LIMIT 1
                     """,
                readRowFunc: reader => new AgentToolScopeOverride(
                    IsEnabled: ToNullableBool(reader.GetInt32OrNull(0)),
                    RequiresApproval: ToNullableBool(reader.GetInt32OrNull(1))))
            .WithParameter("$agentId", agentId)
            .WithParameter("$boxId", boxId)
            .WithParameter("$toolName", toolName)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static bool? ToNullableBool(int? value) =>
        value is null ? null : value != 0;
}
