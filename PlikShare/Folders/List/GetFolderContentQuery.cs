using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Folders.List;

public class GetFolderContentQuery(PlikShareDb plikShareDb)
{
    public readonly record struct ExecutionFlags(
		bool GetCurrentFolder,
		bool GetSubfolders,
		FilesExecutionFlag GetFiles,
		bool GetUploads);
	
	public enum FilesExecutionFlag
	{
		All = 0,
		UploadedByUserOnly
	}

    public GetFolderContentResponseDto? Execute(
	    WorkspaceContext workspace,
	    FolderExtId folderExternalId,
	    int? boxFolderId,
	    IUserIdentity userIdentity,
	    ExecutionFlags executionFlags)
    {
	    using var connection = plikShareDb.OpenConnection();

		//todo what to do with this isEmpty?
	    var (isCurrentFolderEmpty, currentFolderId) = TryGetCurrentFolderId(
		    workspace, 
		    folderExternalId,
		    boxFolderId, 
		    connection);

        if (isCurrentFolderEmpty)
            return null;

        var currentFolder = GetCurrentFolder(
            workspace,
            boxFolderId,
            currentFolderId,
            connection,
            executionFlags.GetCurrentFolder);

        var subfolders = GetSubfolders(
            currentFolderId,
            userIdentity,
            connection,
            executionFlags.GetSubfolders);
        
        var allFiles = GetAllFiles(
            workspace,
            userIdentity,
            currentFolderId,
            connection,
            executionFlags.GetFiles == FilesExecutionFlag.All);
        
        var filesUploadedByUser = GetFilesUploadedByUser(
            workspace,
            userIdentity,
            currentFolderId,
            connection,
            executionFlags.GetFiles == FilesExecutionFlag.UploadedByUserOnly);
        
        var uploads = GetUploads(
            workspace,
            userIdentity,
            currentFolderId,
            connection,
            executionFlags.GetUploads);

        return new GetFolderContentResponseDto
        {
            Folder = currentFolder,
            Files = [..allFiles, ..filesUploadedByUser],
            Subfolders = subfolders,
            Uploads = uploads
        };
    }

    private static List<UploadDto> GetUploads(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity,
	    int currentFolderId,
	    SqliteConnection connection,
        bool shouldGetUploads)
    {
        if (!shouldGetUploads)
            return [];

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
							AND fu_folder_id = $folderId
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
		    .WithParameter("$folderId", currentFolderId)
		    .Execute();
    }

    private static List<FileDto> GetFilesUploadedByUser(
	    WorkspaceContext workspace, 
	    IUserIdentity userIdentity, 
	    int currentFolderId, 
	    SqliteConnection connection, 
        bool shouldGetFilesUploadedByUser)
    {
        if (!shouldGetFilesUploadedByUser)
            return [];

	    return connection
		    .Cmd(
			    sql: @"
						SELECT
				            fi_external_id,
				            fi_name,
				            fi_extension,
				            fi_size_in_bytes,
							TRUE AS fi_was_uploaded_by_user,
                            NOT fi_is_upload_completed 
				        FROM fi_files
				        WHERE
				            fi_workspace_id = $workspaceId
							AND fi_uploader_identity_type = $uploaderIdentityType 
							AND fi_uploader_identity =  $uploaderIdentity
				            AND fi_folder_id = $folderId
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
		    .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
		    .WithParameter("$uploaderIdentity", userIdentity.Identity)
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$folderId", currentFolderId)
		    .Execute();
    }

    private static List<FileDto> GetAllFiles(
	    WorkspaceContext workspace, 
	    IUserIdentity userIdentity, 
	    int currentFolderId, 
	    SqliteConnection connection,
        bool shouldGetAllFiles)
    {
        if (!shouldGetAllFiles)
            return [];

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
				        AND fi_folder_id = $folderId
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
		    .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
		    .WithParameter("$uploaderIdentity", userIdentity.Identity)
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$folderId", currentFolderId)
		    .Execute();
    }

    private static CurrentFolderDto? GetCurrentFolder(
	    WorkspaceContext workspace, 
	    int? boxFolderId, 
	    int currentFolderId,
	    SqliteConnection connection,
        bool shouldGetCurrentFolder)
    {
        if (!shouldGetCurrentFolder)
            return null;

	    var result = connection
		    .OneRowCmd(
			    sql: @"
					SELECT 
						cf.fo_external_id, 
						cf.fo_name,
						(
						    SELECT json_group_array(json_object(
						        'name', af.fo_name,
						        'externalId', af.fo_external_id
						    )) AS fo_ancestors
						    FROM fo_folders AS af
						    WHERE
						        af.fo_id IN (
						            SELECT value FROM json_each(cf.fo_ancestor_folder_ids)
						        )						          
						        AND af.fo_workspace_id = $workspaceId
						        AND af.fo_is_being_deleted = FALSE
							    AND (
			                        $boxFolderId IS NULL 
			                        OR $boxFolderId IN (
			                            SELECT value FROM json_each(af.fo_ancestor_folder_ids) 
			                        )
			                    )
						    ORDER BY json_array_length(af.fo_ancestor_folder_ids)
						) AS fo_ancestors
					FROM fo_folders AS cf
					WHERE 
						cf.fo_id = $folderId
						AND (
							$boxFolderId IS NULL
							OR $boxFolderId IN (
							    SELECT value FROM json_each(cf.fo_ancestor_folder_ids)
							)
						)
				",
				readRowFunc: reader => new CurrentFolderDto
                {
                    ExternalId = reader.GetString(0),
					Name = reader.GetString(1),
					Ancestors = reader.GetFromJson<List<AncestorFolderDto>>(2)
                })
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$folderId", currentFolderId)
		    .WithParameter("$boxFolderId", boxFolderId)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }

    private static List<SubfolderDto> GetSubfolders(
	    int currentFolderId,
	    IUserIdentity userIdentity, 
	    SqliteConnection connection,
        bool shouldGetSubfolders)
    {
        if (!shouldGetSubfolders)
            return [];

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
			        FROM fo_folders
			        WHERE
			            fo_parent_folder_id = $parentFolderId
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
		    .WithParameter("$parentFolderId", currentFolderId)
		    .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
		    .WithParameter("$creatorIdentity", userIdentity.Identity)
		    .Execute();
    }

    private static SQLiteOneRowCommandResult<int> TryGetCurrentFolderId(
	    WorkspaceContext workspace, 
	    FolderExtId folderExternalId, 
	    int? boxFolderId, 
	    SqliteConnection connection)
    {
	    return connection
		    .OneRowCmd(
			    sql: @"
					SELECT fo_id
					FROM fo_folders
					WHERE 
					    fo_external_id = $folderExternalId
					    AND fo_workspace_id = $workspaceId
					    AND fo_is_being_deleted = FALSE
					    AND (
                            $boxFolderId IS NULL 
                            OR $boxFolderId = fo_id 
                            OR $boxFolderId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids) 
                            )
                        )
				",
			    readRowFunc: reader => reader.GetInt32(0))
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$folderExternalId", folderExternalId.Value)
		    .WithParameter("$boxFolderId", boxFolderId)
		    .Execute();
    }
}