using System.Text.Json;
using PlikShare.Agents.Id;
using PlikShare.Agents.Operations.Id;
using PlikShare.Agents.Operations.List.Contracts;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;

namespace PlikShare.Agents.Operations.List;

/// <summary>
/// Lists the still-actionable approval requests for a given approver — the operations that are
/// pending and not yet expired, raised by agents the user owns. Drives the global approval inbox
/// (and the banner that surfaces it).
/// </summary>
public class GetPendingAgentOperationsQuery(
    PlikShareDb plikShareDb,
    IClock clock)
{
    public GetPendingAgentOperationsResponseDto Execute(int ownerUserId)
    {
        using var connection = plikShareDb.OpenConnection();

        var items = connection
            .Cmd(
                sql: """
                     SELECT
                         aop.aop_external_id,
                         aop.aop_tool_name,
                         aop.aop_params_json,
                         aop.aop_created_at,
                         aop.aop_expires_at,
                         a.a_external_id,
                         a.a_name,
                         w.w_external_id,
                         w.w_name
                     FROM aop_agent_operations AS aop
                     INNER JOIN a_agents AS a
                         ON a.a_id = aop.aop_agent_id
                     LEFT JOIN w_workspaces AS w
                         ON w.w_id = aop.aop_workspace_id
                     WHERE aop.aop_status = $pending
                       AND aop.aop_expires_at > $now
                       AND a.a_owner_user_id = $ownerUserId
                     ORDER BY aop.aop_created_at ASC
                     """,
                readRowFunc: reader =>
                {
                    var workspaceExternalId = reader.GetExtIdOrNull<WorkspaceExtId>(7);

                    return new GetPendingAgentOperationsResponseDto.Item
                    {
                        ExternalId = reader.GetExtId<AgentOperationExtId>(0),
                        ToolName = reader.GetString(1),
                        Parameters = JsonDocument.Parse(reader.GetString(2)).RootElement.Clone(),
                        CreatedAt = reader.GetFieldValue<DateTimeOffset>(3),
                        ExpiresAt = reader.GetFieldValue<DateTimeOffset>(4),
                        Agent = new GetPendingAgentOperationsResponseDto.Agent
                        {
                            ExternalId = reader.GetExtId<AgentExtId>(5),
                            Name = reader.GetString(6)
                        },
                        Workspace = workspaceExternalId is null
                            ? null
                            : new GetPendingAgentOperationsResponseDto.Workspace
                            {
                                ExternalId = workspaceExternalId.Value,
                                Name = reader.GetString(8)
                            }
                    };
                })
            .WithParameter("$pending", AgentOperationStatuses.Pending)
            .WithParameter("$now", clock.UtcNow)
            .WithParameter("$ownerUserId", ownerUserId)
            .Execute();

        return new GetPendingAgentOperationsResponseDto
        {
            Items = items
        };
    }
}
