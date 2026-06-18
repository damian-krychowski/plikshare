using PlikShare.Agents.Cache;
using PlikShare.Agents.Id;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.Tools;

/// <summary>
/// Lists the overridable tools for a single (agent, box) pair, surfacing the agent's global config, any
/// per-box override and the effective result. Box overrides apply to the same set of finer-scopable
/// tools as workspace overrides (<see cref="AgentToolDefinition.IsWorkspaceOverridable"/>).
/// </summary>
public class GetAgentBoxToolsQuery(
    AgentCache agentCache,
    PlikShareDb plikShareDb)
{
    public async Task<Result> Execute(
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        var agent = await agentCache.TryGetAgent(
            agentExternalId,
            cancellationToken);

        if (agent is null)
            return new Result(ResultCode.AgentNotFound);

        using var connection = plikShareDb.OpenConnection();

        var boxId = connection
            .OneRowCmd(
                sql: """
                     SELECT bo_id
                     FROM bo_boxes
                     WHERE bo_external_id = $externalId
                         AND bo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", boxExternalId.Value)
            .Execute();

        if (boxId.IsEmpty)
            return new Result(ResultCode.BoxNotFound);

        var overrides = connection
            .Cmd(
                sql: """
                     SELECT
                         atbo_tool_name,
                         atbo_is_enabled,
                         atbo_requires_approval
                     FROM atbo_agent_tool_box_overrides
                     WHERE atbo_agent_id = $agentId
                         AND atbo_box_id = $boxId
                     """,
                readRowFunc: reader => new
                {
                    ToolName = reader.GetString(0),
                    IsEnabled = ToNullableBool(reader.GetInt32OrNull(1)),
                    RequiresApproval = ToNullableBool(reader.GetInt32OrNull(2))
                })
            .WithParameter("$agentId", agent.Id)
            .WithParameter("$boxId", boxId.Value)
            .Execute()
            .ToDictionary(
                row => row.ToolName,
                row => new AgentToolScopeOverride(row.IsEnabled, row.RequiresApproval));

        var tools = AgentToolCatalog.All
            .Where(definition => definition.IsWorkspaceOverridable)
            .Select(definition => BuildDto(
                agent,
                definition,
                overrides.GetValueOrDefault(definition.Name)))
            .ToList();

        return new Result(
            ResultCode.Ok,
            new GetAgentBoxToolsResponseDto { Tools = tools });
    }

    private static AgentBoxToolConfigDto BuildDto(
        AgentContext agent,
        AgentToolDefinition definition,
        AgentToolScopeOverride? boxOverride)
    {
        var global = AgentToolCatalog.Resolve(agent, definition);
        var effective = AgentToolCatalog.Resolve(agent, definition, workspaceOverride: null, boxOverride: boxOverride);

        return new AgentBoxToolConfigDto
        {
            Name = definition.Name,
            Description = definition.Description,
            GlobalIsEnabled = global.IsEnabled,
            GlobalRequiresApproval = global.RequiresApproval,
            OverrideIsEnabled = boxOverride?.IsEnabled,
            OverrideRequiresApproval = boxOverride?.RequiresApproval,
            EffectiveIsEnabled = effective.IsEnabled,
            EffectiveRequiresApproval = effective.RequiresApproval
        };
    }

    private static bool? ToNullableBool(int? value) =>
        value is { } number ? number != 0 : null;

    public enum ResultCode
    {
        Ok = 0,
        AgentNotFound,
        BoxNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        GetAgentBoxToolsResponseDto? Response = null);
}
