using PlikShare.Agents.Cache;
using PlikShare.Agents.Tools;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.Search;

/// <summary>
/// search runs across every workspace the agent can access, so it has no single workspace to override.
/// Instead the per-workspace override is folded into the search: a workspace whose effective search
/// config is disabled drops out of the scope, and if any in-scope workspace requires approval the whole
/// search requires approval. This resolver computes that scope from the agent's accessible workspaces
/// and their per-workspace search overrides.
/// </summary>
public class AgentSearchScopeResolver(
    PlikShareDb plikShareDb)
{
    public SearchScope Resolve(AgentContext agent)
    {
        var definition = AgentToolCatalog.TryGet(AgentToolNames.Search)!;

        var workspaces = GetAccessibleWorkspacesWithOverride(agent);

        var disabled = new List<string>();
        var anyEnabled = false;
        var requiresApproval = false;

        foreach (var workspace in workspaces)
        {
            var effective = AgentToolCatalog.Resolve(agent, definition, workspace.Override);

            if (!effective.IsUsable)
            {
                disabled.Add(workspace.ExternalId);
                continue;
            }

            anyEnabled = true;

            if (effective.RequiresApproval)
                requiresApproval = true;
        }

        return new SearchScope(
            AnyEnabled: anyEnabled,
            RequiresApproval: requiresApproval,
            DisabledWorkspaceExternalIds: disabled);
    }

    private List<(string ExternalId, AgentToolScopeOverride? Override)> GetAccessibleWorkspacesWithOverride(
        AgentContext agent)
    {
        using var connection = plikShareDb.OpenConnection();

        var accessClause = agent.HasAdminRole
            ? ""
            : """
                  AND (
                      w.w_owner_agent_id = $agentId
                      OR EXISTS (
                          SELECT 1
                          FROM wa_workspace_agents
                          WHERE wa_workspace_id = w.w_id
                              AND wa_agent_id = $agentId
                      )
                  )
              """;

        var sql = $"""
                   SELECT
                       w.w_external_id,
                       o.atwo_is_enabled,
                       o.atwo_requires_approval
                   FROM w_workspaces AS w
                   LEFT JOIN atwo_agent_tool_workspace_overrides AS o
                       ON o.atwo_workspace_id = w.w_id
                       AND o.atwo_agent_id = $agentId
                       AND o.atwo_tool_name = $toolName
                   WHERE w.w_is_being_deleted = FALSE
                       {accessClause}
                   ORDER BY w.w_id ASC
                   """;

        return connection
            .Cmd(
                sql: sql,
                readRowFunc: reader =>
                {
                    var externalId = reader.GetExtId<WorkspaceExtId>(0).Value;
                    var isEnabled = reader.GetInt32OrNull(1);
                    var requiresApproval = reader.GetInt32OrNull(2);

                    AgentToolScopeOverride? scopeOverride =
                        isEnabled is null && requiresApproval is null
                            ? null
                            : new AgentToolScopeOverride(
                                IsEnabled: ToNullableBool(isEnabled),
                                RequiresApproval: ToNullableBool(requiresApproval));

                    return (ExternalId: externalId, Override: scopeOverride);
                })
            .WithParameter("$agentId", agent.Id)
            .WithParameter("$toolName", AgentToolNames.Search)
            .Execute();
    }

    private static bool? ToNullableBool(int? value) =>
        value is null ? null : value != 0;

    public readonly record struct SearchScope(
        bool AnyEnabled,
        bool RequiresApproval,
        List<string> DisabledWorkspaceExternalIds);
}
