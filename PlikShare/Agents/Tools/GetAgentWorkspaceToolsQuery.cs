using PlikShare.Agents.Cache;
using PlikShare.Agents.Id;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;

namespace PlikShare.Agents.Tools;

public class GetAgentWorkspaceToolsQuery(
    AgentCache agentCache,
    PlikShareDb plikShareDb)
{
    public async Task<Result> Execute(
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId,
        CancellationToken cancellationToken)
    {
        var agent = await agentCache.TryGetAgent(
            agentExternalId,
            cancellationToken);

        if (agent is null)
            return new Result(ResultCode.AgentNotFound);

        using var connection = plikShareDb.OpenConnection();

        var workspaceId = connection
            .OneRowCmd(
                sql: """
                     SELECT w_id
                     FROM w_workspaces
                     WHERE w_external_id = $externalId
                         AND w_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", workspaceExternalId.Value)
            .Execute();

        if (workspaceId.IsEmpty)
            return new Result(ResultCode.WorkspaceNotFound);

        var overrides = connection
            .Cmd(
                sql: """
                     SELECT
                         atwo_tool_name,
                         atwo_is_enabled,
                         atwo_requires_approval
                     FROM atwo_agent_tool_workspace_overrides
                     WHERE atwo_agent_id = $agentId
                         AND atwo_workspace_id = $workspaceId
                     """,
                readRowFunc: reader => new
                {
                    ToolName = reader.GetString(0),
                    IsEnabled = ToNullableBool(reader.GetInt32OrNull(1)),
                    RequiresApproval = ToNullableBool(reader.GetInt32OrNull(2))
                })
            .WithParameter("$agentId", agent.Id)
            .WithParameter("$workspaceId", workspaceId.Value)
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
            new GetAgentWorkspaceToolsResponseDto { Tools = tools });
    }

    private static AgentWorkspaceToolConfigDto BuildDto(
        AgentContext agent,
        AgentToolDefinition definition,
        AgentToolScopeOverride? workspaceOverride)
    {
        var global = AgentToolCatalog.Resolve(agent, definition);
        var effective = AgentToolCatalog.Resolve(agent, definition, workspaceOverride);

        return new AgentWorkspaceToolConfigDto
        {
            Name = definition.Name,
            Description = definition.Description,
            IsAvailable = effective.IsAvailable,
            GlobalIsEnabled = global.IsEnabled,
            GlobalRequiresApproval = global.RequiresApproval,
            OverrideIsEnabled = workspaceOverride?.IsEnabled,
            OverrideRequiresApproval = workspaceOverride?.RequiresApproval,
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
        WorkspaceNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        GetAgentWorkspaceToolsResponseDto? Response = null);
}
