using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using PlikShare.Integrations.List.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.Integrations.List;

public class GetIntegrationsQuery(PlikShareDb plikShareDb)
{
    public GetIntegrationsResponseDto Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        var integrations = connection
            .Cmd(
                sql: @"
                    SELECT 
                        i_external_id,
                        i_name,
                        i_type,
                        i_is_active,
                        w_external_id,
                        w_name
                    FROM i_integrations
                    INNER JOIN w_workspaces
                        ON w_id = i_workspace_id
                    ORDER BY i_id ASC
                ",
                readRowFunc: reader => new GetIntegrationsItemResponseDto
                {
                    ExternalId = reader.GetExtId<IntegrationExtId>(0),
                    Name = reader.GetString(1),
                    Type = reader.GetEnum<IntegrationType>(2),
                    IsActive = reader.GetBoolean(3),
                    Workspace = new IntegrationWorkspaceDto
                    {
                        ExternalId = reader.GetExtId<WorkspaceExtId>(4),
                        Name = reader.GetString(5)
                    }
                })
            .Execute();

        return new GetIntegrationsResponseDto
        {
            Items = integrations
        };
    }
}