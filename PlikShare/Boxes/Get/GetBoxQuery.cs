using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Boxes.Get;

public class GetBoxQuery(PlikShareDb plikShareDb)
{
    public GetBoxResponseDto Execute(
	    BoxContext box)
    {
	    using var connection = plikShareDb.OpenConnection();

	    var details = GetBoxDetails(
		    box,
		    connection);
        
	    var links = GetLinks(
		    box,
		    connection);
        
	    var members = GetMembers(
		    box, 
		    connection);

        if (box.Folder is null)
            return new GetBoxResponseDto
            {
                Details = details,
                Links = links,
                Members = members,
                Files = null,
                Subfolders = null
            };
	    
	    var subfolders = GetSubfolders(
		    box, 
		    connection);
        
	    var files = GetFiles(
		    box, 
		    connection);

        return new GetBoxResponseDto
        {
            Details = details,
            Links = links,
            Members = members,
            Files = files,
            Subfolders = subfolders
        };
    }

    private static List<GetBoxResponseDto.File> GetFiles(
	    BoxContext box, 
	    SqliteConnection connection)
    {
	    return connection
		    .Cmd(
			    sql: """
                     SELECT
                      	fi_external_id,
                      	fi_name,
                      	fi_extension,
                      	fi_size_in_bytes
                     FROM fi_files
                     WHERE 
                      	fi_folder_id = $boxFolderId
                      	AND fi_workspace_id = $workspaceId
                     ORDER BY 
                      	fi_id DESC
                     """,
                readRowFunc: reader => new GetBoxResponseDto.File
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Extension = reader.GetString(2),
                    SizeInBytes = reader.GetInt64(3)
                })
		    .WithParameter("$boxFolderId", box.Folder!.Id)
		    .WithParameter("$workspaceId", box.Workspace.Id)
		    .Execute();
    }

    private static List<GetBoxResponseDto.Subfolder> GetSubfolders(
	    BoxContext box, 
	    SqliteConnection connection)
    {
	    return connection
		    .Cmd(
			    sql: """
                     SELECT
                      	fo_external_id,
                      	fo_name
                     FROM fo_folders
                     WHERE
                      	fo_parent_folder_id = $boxFolderId
                      	AND fo_workspace_id = $workspaceId
                      	AND fo_is_being_deleted = FALSE
                     ORDER BY 
                      	fo_id
                     """,
			    readRowFunc: reader => new GetBoxResponseDto.Subfolder
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1)
                })
		    .WithParameter("$boxFolderId", box.Folder!.Id)
		    .WithParameter("$workspaceId", box.Workspace.Id)
		    .Execute();
    }

    private static List<GetBoxResponseDto.Member> GetMembers(
	    BoxContext box, 
	    SqliteConnection connection)
    {
	    return connection
		    .Cmd(
			    sql: """
                     SELECT
                      	um.u_external_id AS member_external_id,
                      	um.u_email AS member_email,
                      	bm_was_invitation_accepted,
                      	bm_allow_download,
                      	bm_allow_upload,
                      	bm_allow_list,
                      	bm_allow_delete_file,
                      	bm_allow_rename_file,
                      	bm_allow_move_items,
                      	bm_allow_create_folder,
                      	bm_allow_delete_folder,
                      	bm_allow_rename_folder,
                      	ui.u_email AS inviter_email
                     FROM bm_box_membership
                     INNER JOIN u_users AS ui
                      	ON ui.u_id = bm_inviter_id
                     INNER JOIN u_users AS um
                      	ON um.u_id = bm_member_id
                     WHERE bm_box_id = $boxId
                     """,
                readRowFunc: reader => new GetBoxResponseDto.Member
                {
                    MemberExternalId = reader.GetString(0),
                    MemberEmail = reader.GetString(1),
                    WasInvitationAccepted = reader.GetBoolean(2),
                    Permissions = new GetBoxResponseDto.Permissions
                    {
                        AllowDownload = reader.GetBoolean(3),
                        AllowUpload = reader.GetBoolean(4),
                        AllowList = reader.GetBoolean(5),
                        AllowDeleteFile = reader.GetBoolean(6),
                        AllowRenameFile = reader.GetBoolean(7),
                        AllowMoveItems = reader.GetBoolean(8),
                        AllowCreateFolder = reader.GetBoolean(9),
                        AllowDeleteFolder = reader.GetBoolean(10),
                        AllowRenameFolder = reader.GetBoolean(11)
                    },
                    InviterEmail = reader.GetString(12)
                })
		    .WithParameter("$boxId", box.Id)
		    .Execute();
    }

    private static List<GetBoxResponseDto.BoxLink> GetLinks(
	    BoxContext box, 
	    SqliteConnection connection)
    {
	    return connection
		    .Cmd(
			    sql: """
                     SELECT
                        bl_external_id,
                        bl_is_enabled,
                        bl_name,
                        bl_access_code,
                        bl_allow_download,
                        bl_allow_upload,
                        bl_allow_list,
                        bl_allow_delete_file,
                        bl_allow_rename_file,
                        bl_allow_move_items,
                        bl_allow_create_folder,
                        bl_allow_delete_folder,
                        bl_allow_rename_folder,
                        bl_widget_origins
                     FROM bl_box_links
                     WHERE bl_box_id = $boxId
                     ORDER BY bl_id ASC
                     """,
                readRowFunc: reader => new GetBoxResponseDto.BoxLink
                {
                    ExternalId = reader.GetString(0),
					IsEnabled = reader.GetBoolean(1),
					Name = reader.GetString(2),
					AccessCode = reader.GetString(3),
					Permissions = new GetBoxResponseDto.Permissions
                    {
                        AllowDownload = reader.GetBoolean(4),
                        AllowUpload = reader.GetBoolean(5),
						AllowList = reader.GetBoolean(6),
						AllowDeleteFile = reader.GetBoolean(7),
						AllowRenameFile = reader.GetBoolean(8),
						AllowMoveItems = reader.GetBoolean(9),
						AllowCreateFolder = reader.GetBoolean(10),
						AllowDeleteFolder = reader.GetBoolean(11),
						AllowRenameFolder = reader.GetBoolean(12)
                    },
                    WidgetOrigins = reader.GetFromJsonOrNull<List<string>>(13) ?? []
                })
		    .WithParameter("$boxId", box.Id)
		    .Execute();
    }

    private static GetBoxResponseDto.BoxDetails GetBoxDetails(
	    BoxContext box, 
	    SqliteConnection connection)
    {
	    return connection
		    .OneRowCmd(
			    sql: """
                        SELECT
                     	    bo_external_id,
                     	    bo_name,
                     	    bo_is_enabled,
                     	    bo_header_is_enabled,
                     	    bo_header_json,
                     	    bo_footer_is_enabled,
                     	    bo_footer_json,
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
                     	    AND fo.fo_is_being_deleted = FALSE
                        WHERE bo_id = $boxId
                        LIMIT 1
                     """,
                readRowFunc: reader =>
                {
                    var externalId = reader.GetString(0);
                    var name = reader.GetString(1);
                    var isEnabled = reader.GetBoolean(2);
                    var isHeaderEnabled = reader.GetBoolean(3);
                    var headerJson = reader.GetStringOrNull(4);
                    var isFooterEnabled = reader.GetBoolean(5);
                    var footerJson = reader.GetStringOrNull(6);
                    var folderPath = reader.GetFromJson<List<GetBoxResponseDto.FolderItem>>(7);

                    return new GetBoxResponseDto.BoxDetails
                    {
                        ExternalId = externalId,
                        Name = name,
                        IsEnabled = isEnabled,
						Header = new GetBoxResponseDto.Section
                        {
                            IsEnabled = isHeaderEnabled,
							Json = headerJson
                        },
						Footer = new GetBoxResponseDto.Section
                        {
                            IsEnabled = isFooterEnabled,
							Json = footerJson
                        },
						FolderPath = folderPath
                    };
                })
		    .WithParameter("$boxId", box.Id)
		    .ExecuteOrThrow();
    }
}