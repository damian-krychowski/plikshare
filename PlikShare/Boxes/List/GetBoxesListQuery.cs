using PlikShare.Boxes.Id;
using PlikShare.Boxes.List.Contracts;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Boxes.List;

public class GetBoxesListQuery(PlikShareDb plikShareDb)
{	
    public GetBoxesResponseDto Execute(
	    WorkspaceContext workspace)
    {
	    using var connection = plikShareDb.OpenConnection();
	    
	    var boxes = connection
		    .Cmd(
			    sql: """
                     SELECT
                     	bo_external_id,
                     	bo_name,
                     	bo_is_enabled,
                     	(CASE
                     		 WHEN bo_folder_id IS NULL THEN '[]'
                             ELSE (
                     			SELECT json_group_array(
                     				json_object(
                     					'name', af.fo_name,
                     					'externalId', af.fo_external_id
                     				)
                     			)
                     			FROM fo_folders AS af
                     			WHERE
                     				 af.fo_id IN (
                     					SELECT value FROM json_each(fo.fo_ancestor_folder_ids)
                     					UNION ALL
                     					SELECT fo.fo_id
                     				)
                     		        AND af.fo_is_being_deleted = FALSE	
                     		)    
                     	END) AS bo_folder_path
                     FROM bo_boxes
                     LEFT JOIN fo_folders AS fo
                     ON fo.fo_id = bo_folder_id 
                     	AND fo.fo_workspace_id = $workspaceId
                     	AND fo.fo_is_being_deleted = FALSE
                     WHERE
                     	bo_workspace_id = $workspaceId
                     	AND bo_is_being_deleted = FALSE
                     ORDER BY 
                     	bo_id ASC
                     """,
                readRowFunc: reader => new GetBoxesResponseDto.Box
                {
                    ExternalId = reader.GetExtId<BoxExtId>(0),
                    Name = reader.GetString(1),
                    IsEnabled = reader.GetBoolean(2),
                    FolderPath = reader.GetFromJson<List<GetBoxesResponseDto.FolderItem>>(3)
                })
		    .WithParameter("$workspaceId", workspace.Id)
		    .Execute();

        return new GetBoxesResponseDto
        {
            Items = boxes
        };
    }
}