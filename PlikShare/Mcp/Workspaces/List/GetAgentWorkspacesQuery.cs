using PlikShare.Agents.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Mcp.Workspaces.List.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.Workspaces.List;

public class GetAgentWorkspacesQuery(
    PlikShareDb plikShareDb,
    WorkspaceSizeCache workspaceSizeCache)
{
    public List<WorkspaceItemDto> Execute(AgentContext agent)
    {
        var workspaces = GetWorkspaces(agent);

        return workspaces
            .Select(workspace => new WorkspaceItemDto
            {
                WorkspaceExternalId = workspace.ExternalId,
                Name = workspace.Name,
                CurrentSizeInBytes = workspaceSizeCache.Get(workspace.Id)
            })
            .ToList();
    }

    private List<(int Id, string ExternalId, string Name)> GetWorkspaces(AgentContext agent)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT w_id, w_external_id, w_name
                     FROM w_workspaces
                     WHERE w_is_being_deleted = FALSE
                         AND (
                             w_owner_agent_id = $agentId
                             OR EXISTS (
                                 SELECT 1
                                 FROM wa_workspace_agents
                                 WHERE wa_workspace_id = w_id
                                     AND wa_agent_id = $agentId
                             )
                         )
                     ORDER BY w_id ASC
                     """,
                readRowFunc: reader => (
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<WorkspaceExtId>(1).Value,
                    Name: reader.GetString(2)))
            .WithParameter("$agentId", agent.Id)
            .Execute();
    }
}
