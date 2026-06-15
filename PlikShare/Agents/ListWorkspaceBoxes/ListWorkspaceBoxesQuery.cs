using PlikShare.Agents.ListWorkspaceBoxes.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;

namespace PlikShare.Agents.ListWorkspaceBoxes;

public class ListWorkspaceBoxesQuery(PlikShareDb plikShareDb)
{
    public ListWorkspaceBoxesResponseDto Execute(
        WorkspaceExtId workspaceExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var items = connection
            .Cmd(
                sql: """
                     SELECT
                         bo_external_id,
                         bo_name
                     FROM bo_boxes
                     INNER JOIN w_workspaces
                         ON w_id = bo_workspace_id
                     WHERE w_external_id = $workspaceExternalId
                         AND bo_is_being_deleted = FALSE
                         AND w_is_being_deleted = FALSE
                     ORDER BY bo_name ASC
                     """,
                readRowFunc: reader => new ListWorkspaceBoxesResponseDto.Box
                {
                    ExternalId = reader.GetExtId<BoxExtId>(0),
                    Name = reader.GetString(1)
                })
            .WithParameter("$workspaceExternalId", workspaceExternalId.Value)
            .Execute();

        return new ListWorkspaceBoxesResponseDto
        {
            Items = items
        };
    }
}
