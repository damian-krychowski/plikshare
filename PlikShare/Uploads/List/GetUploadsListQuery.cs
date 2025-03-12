using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.List.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Uploads.List;

public class GetUploadsListQuery(PlikShareDb plikShareDb)
{
    public GetUploadsListResponseDto Execute(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity,
	    int? boxFolderId)
    {
	    using var connection = plikShareDb.OpenConnection();

	    var uploads = connection
		    .Cmd(
			    sql: """
			        SELECT
			            fu_external_id,
			            fu_file_name,
			            fu_file_extension,
			            fu_file_content_type,
			            fu_file_size_in_bytes,
			            fo.fo_external_id,
			            fo.fo_name, 
			            (CASE 
			         	    WHEN fu_folder_id IS NULL THEN '[]'
			         	    ELSE (
			         		    SELECT json_group_array(foa.fo_name)
			         		    FROM (
			         			    SELECT 
			         				    value AS ancestor_id,
			         				    rowid AS ord
			         			    FROM json_each(fo.fo_ancestor_folder_ids)
			         		    ) AS u
			         		    INNER JOIN fo_folders foa
			         			    ON foa.fo_id = u.ancestor_id
			         			    AND foa.fo_workspace_id = $workspaceId
			         		    WHERE foa.fo_workspace_id = $workspaceId
			         			    AND foa.fo_is_being_deleted = FALSE
			         			    AND (
			         				    $boxFolderId IS NULL 
			         				    OR $boxFolderId = foa.fo_id 
			         				    OR $boxFolderId IN (
			         					    SELECT value FROM json_each(foa.fo_ancestor_folder_ids)
			         				    )
			         			    )
			         		    ORDER BY
			         			    u.ord
			         	    )
			            END) AS folder_path,
			            (	
			        	    SELECT json_group_array(fup_part_number)
			        	    FROM fup_file_upload_parts
			        	    WHERE fup_file_upload_id = fu_id
			        	    ORDER BY fup_part_number
			            ) AS fu_already_uploaded_part_numbers
			        FROM fu_file_uploads
			        LEFT JOIN fo_folders AS fo
			            ON fo.fo_id = fu_folder_id 
			            AND fo.fo_workspace_id = $workspaceId
			            AND fo.fo_is_being_deleted = FALSE
			        WHERE
			         	fu_workspace_id = $workspaceId
			         	AND fu_owner_identity_type = $ownerIdentityType
			         	AND fu_owner_identity = $ownerIdentity
			            AND fu_is_completed = FALSE
			        ORDER BY 
			        fu_id DESC
			        """,
                readRowFunc: reader => new GetUploadsListResponseDto.Upload
                {
                    ExternalId = reader.GetExtId<FileUploadExtId>(0),
					FileName = reader.GetString(1),
					FileExtension = reader.GetString(2),
					FileContentType = reader.GetString(3),
					FileSizeInBytes = reader.GetInt64(4),
					FolderExternalId = reader.GetExtIdOrNull<FolderExtId>(5),
					FolderName = reader.GetStringOrNull(6),
					FolderPath = reader.GetFromJsonOrNull<List<string>>(7),
					AlreadyUploadedPartNumbers = reader.GetFromJson<List<int>>(8)
                })
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$boxFolderId", boxFolderId)
		    .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
		    .WithParameter("$ownerIdentity", userIdentity.Identity)
		    .Execute();

        return new GetUploadsListResponseDto
        {
            Items = uploads
        };
    }
}