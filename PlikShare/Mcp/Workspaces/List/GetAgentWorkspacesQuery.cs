using PlikShare.Agents.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Mcp.Workspaces.List.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.Workspaces.List;

public class GetAgentWorkspacesQuery(PlikShareDb plikShareDb)
{
    public List<WorkspaceItemDto> Execute(AgentContext agent)
    {
        using var connection = plikShareDb.OpenConnection();

        if (agent.HasAdminRole)
        {
            return connection
                .Cmd(
                    sql: """
                         SELECT w_external_id, w_name
                         FROM w_workspaces
                         WHERE w_is_being_deleted = FALSE
                         ORDER BY w_id ASC
                         """,
                    readRowFunc: reader => new WorkspaceItemDto
                    {
                        WorkspaceExternalId = reader.GetExtId<WorkspaceExtId>(0).Value,
                        Name = reader.GetString(1)
                    })
                .Execute();
        }

        return connection
            .Cmd(
                sql: """
                     SELECT w_external_id, w_name
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
                readRowFunc: reader => new WorkspaceItemDto
                {
                    WorkspaceExternalId = reader.GetExtId<WorkspaceExtId>(0).Value,
                    Name = reader.GetString(1)
                })
            .WithParameter("$agentId", agent.Id)
            .Execute();
    }
}
