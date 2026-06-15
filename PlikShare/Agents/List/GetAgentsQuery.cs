using PlikShare.Agents.Id;
using PlikShare.Agents.List.Contracts;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.List;

public class GetAgentsQuery(PlikShareDb plikShareDb)
{
    public GetAgentsResponseDto Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        var items = connection
            .Cmd(
                sql: """
                     SELECT
                         a_external_id,
                         a_name,
                         a_is_enabled,
                         a_created_at
                     FROM a_agents
                     ORDER BY a_id DESC
                     """,
                readRowFunc: reader => new GetAgentsResponseDto.Agent
                {
                    ExternalId = reader.GetExtId<AgentExtId>(0),
                    Name = reader.GetString(1),
                    IsEnabled = reader.GetBoolean(2),
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(3)
                })
            .Execute();

        return new GetAgentsResponseDto
        {
            Items = items
        };
    }
}
