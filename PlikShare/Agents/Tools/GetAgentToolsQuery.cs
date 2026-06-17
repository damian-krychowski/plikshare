using PlikShare.Agents.Cache;
using PlikShare.Agents.Id;
using PlikShare.Agents.Tools.Contracts;

namespace PlikShare.Agents.Tools;

public class GetAgentToolsQuery(AgentCache agentCache)
{
    public async Task<GetAgentToolsResponseDto?> Execute(
        AgentExtId agentExternalId,
        CancellationToken cancellationToken)
    {
        var agent = await agentCache.TryGetAgent(
            agentExternalId,
            cancellationToken);

        if (agent is null)
            return null;

        var tools = AgentToolCatalog.All
            .Select(definition => BuildDto(agent, definition))
            .ToList();

        return new GetAgentToolsResponseDto
        {
            Tools = tools
        };
    }

    private static AgentToolConfigDto BuildDto(AgentContext agent, AgentToolDefinition definition)
    {
        var effective = AgentToolCatalog.Resolve(agent, definition);

        // "Default" means the effective values match the catalog defaults — not merely that no
        // config row exists. Setting a tool back to its default values clears the "customised" flag
        // even if a row lingers in the database.
        var isDefault = effective.IsEnabled == definition.DefaultIsEnabled
            && effective.RequiresApproval == definition.DefaultRequiresApproval;

        return new AgentToolConfigDto
        {
            Name = definition.Name,
            Description = definition.Description,
            Scope = definition.Group == AgentToolGroup.Workspace ? "workspace" : "instance",
            RequiredPermission = definition.RequiredPermission == AgentToolPermission.AddWorkspace
                ? "add_workspace"
                : null,
            IsAvailable = effective.IsAvailable,
            IsEnabled = effective.IsEnabled,
            RequiresApproval = effective.RequiresApproval,
            IsDefault = isDefault
        };
    }
}
