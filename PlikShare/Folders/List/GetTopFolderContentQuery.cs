using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.List.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Folders.List;

public class GetTopFolderContentQuery(PlikShareDb plikShareDb)
{
    public GetTopFolderContentResponseDto Execute(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity)
    {
	    using var connection = plikShareDb.OpenConnection();

	    var folders = GetFolders(
		    workspace,
			userIdentity,
		    connection);
	    
	    var files = GetFiles(
		    workspace,
			userIdentity,
		    connection);

	    var uploads = GetUploads(
		    workspace,
		    userIdentity,
		    connection);

        return new GetTopFolderContentResponseDto
        {
            Files = files,
            Subfolders = folders,
            Uploads = uploads
        };
    }

    private static List<UploadDto> GetUploads(
	    WorkspaceContext workspace, 
	    IUserIdentity userIdentity, 
	    SqliteConnection connection)
    {
	    return connection
		    .Cmd(
			    sql: @"
					SELECT
						fu_external_id,
						fu_file_name,
						fu_file_extension,
						fu_file_content_type,
						fu_file_size_in_bytes,
						(
					        SELECT json_group_array(fup_part_number)
					        FROM fup_file_upload_parts
					        WHERE fup_file_upload_id = fu_id
					        ORDER BY fup_part_number
					    ) AS fu_already_uploaded_part_numbers
					FROM fu_file_uploads
					WHERE
						fu_workspace_id = $workspaceId
		  				AND fu_owner_identity_type = $ownerIdentityType
		  				AND fu_owner_identity = $ownerIdentity
						AND fu_folder_id IS NULL
                        AND fu_is_completed = FALSE
                        AND fu_parent_file_id IS NULL
					ORDER BY 
						fu_id DESC
				",
                readRowFunc: reader => new UploadDto
                {
                    ExternalId = reader.GetString(0),
					FileName = reader.GetString(1),
					FileExtension = reader.GetString(2),
					FileContentType = reader.GetString(3),
					FileSizeInBytes = reader.GetInt64(4),
					AlreadyUploadedPartNumbers = reader.GetFromJson<List<int>>(5)
                })
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
		    .WithParameter("$ownerIdentity", userIdentity.Identity)
		    .Execute();
    }

    private static List<FileDto> GetFiles(
	    WorkspaceContext workspace,
        IUserIdentity userIdentity,
        SqliteConnection connection)
    {
	    return connection
		    .Cmd(
			    sql: @"
					SELECT
		                fi_external_id,
		                fi_name,
		                fi_extension,
		                fi_size_in_bytes,
                        (
							fi_uploader_identity_type = $uploaderIdentityType 
							AND fi_uploader_identity =  $uploaderIdentity
						) AS fi_was_uploaded_by_user,
                        NOT fi_is_upload_completed 
		            FROM fi_files
		            WHERE
		                fi_workspace_id = $workspaceId
		                AND fi_folder_id IS NULL
                        AND fi_parent_file_id IS NULL
					ORDER BY
					    fi_id DESC
				",
                readRowFunc: reader => new FileDto
                {
                    ExternalId = reader.GetString(0),
					Name = reader.GetString(1),
					Extension = reader.GetString(2),
					SizeInBytes = reader.GetInt64(3),
					WasUploadedByUser = reader.GetBoolean(4),
					IsLocked = reader.GetBoolean(5)
                })
		    .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .Execute();
    }

    private static List<SubfolderDto> GetFolders(
	    WorkspaceContext workspace,
        IUserIdentity userIdentity,
        SqliteConnection connection)
    {
	    return connection
		    .Cmd(
			    sql: @"
					SELECT 
		                fo_external_id,
		                fo_name,
                        CASE 
	                        WHEN fo_creator_identity_type = $creatorIdentityType AND fo_creator_identity = $creatorIdentity THEN TRUE
							ELSE FALSE
	                    END AS fo_was_created_by_user,
			            CASE 
	                        WHEN fo_creator_identity_type = $creatorIdentityType AND fo_creator_identity = $creatorIdentity THEN fo_created_at
	                    END AS fo_created_at
		            FROM
		                fo_folders
		            WHERE
		                fo_workspace_id = $workspaceId
		                AND fo_parent_folder_id IS NULL
		                AND fo_is_being_deleted = FALSE
		            ORDER BY 
		                fo_id
				",
                readRowFunc: reader => new SubfolderDto
                {
                    ExternalId = reader.GetString(0),
					Name = reader.GetString(1),
					WasCreatedByUser = reader.GetBoolean(2),
					CreatedAt = reader.GetDateTimeOffsetOrNull(3)?.DateTime
				})
		    .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
            .WithParameter("$creatorIdentity", userIdentity.Identity)
            .Execute();
    }
}