using PlikShare.Agents.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Mcp.BoxAccess.List.Contracts;

namespace PlikShare.Mcp.BoxAccess.List;

/// <summary>
/// Lists the boxes shared directly with an agent — the <c>ba_box_agents</c> rows — together with the name
/// of the workspace each box belongs to. These are the agent's box-access entry points; it can browse and
/// act inside them with the box-access tools regardless of any workspace membership.
/// </summary>
public class GetAgentAccessibleBoxesQuery(PlikShareDb plikShareDb)
{
    public ListBoxesResponseDto Execute(AgentContext agent)
    {
        using var connection = plikShareDb.OpenConnection();

        var boxes = connection
            .Cmd(
                sql: """
                     SELECT
                         bo_external_id,
                         bo_name,
                         bo_is_enabled,
                         w_name
                     FROM ba_box_agents
                     INNER JOIN bo_boxes
                         ON bo_id = ba_box_id
                     INNER JOIN w_workspaces
                         ON w_id = bo_workspace_id
                     WHERE ba_agent_id = $agentId
                         AND bo_is_being_deleted = FALSE
                         AND w_is_being_deleted = FALSE
                     ORDER BY bo_name ASC
                     """,
                readRowFunc: reader => new ListBoxesResponseDto.BoxDto
                {
                    ExternalId = reader.GetExtId<BoxExtId>(0).Value,
                    Name = reader.GetString(1),
                    IsEnabled = reader.GetBoolean(2),
                    WorkspaceName = reader.GetString(3)
                })
            .WithParameter("$agentId", agent.Id)
            .Execute();

        return new ListBoxesResponseDto
        {
            Boxes = boxes
        };
    }
}
