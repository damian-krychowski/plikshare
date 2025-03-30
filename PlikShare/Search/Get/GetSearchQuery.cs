using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Search.Get.Contracts;
using PlikShare.Users.Cache;

namespace PlikShare.Search.Get;

public class GetSearchQuery(PlikShareDb plikShareDb)
{
    public SearchResponseDto Execute(
        UserContext user,
        string[] workspaceExternalIds,
        string[] boxExternalIds,
        string phrase)
    {
        using var connection = plikShareDb.OpenConnection();

        var query = $"%{phrase}%";

        List<SearchResponseDto.WorkspaceGroup>? workspaceGroups = null;
        List<SearchResponseDto.ExternalBoxGroup>? externalBoxGroups = null;
        List<SearchResponseDto.Workspace>? workspaces = null;
        List<SearchResponseDto.WorkspaceFolder>? workspaceFolders = null;
        List<SearchResponseDto.WorkspaceBox>? workspaceBoxes = null;
        List<SearchResponseDto.WorkspaceFile>? workspaceFiles = null;
        List<SearchResponseDto.ExternalBox>? externalBoxes = null;
        List<SearchResponseDto.ExternalBoxFolder>? externalBoxFolders = null;
        List<SearchResponseDto.ExternalBoxFile>? externalBoxFiles = null;
        
        var workspaceGroupExternalIds = new HashSet<string>();
        var externalBoxGroupExternalIds = new HashSet<string>();

        var userWorkspaces = GetWorkspacesAvailableToUser(
            user,
            workspaceExternalIds,
            connection);

        var userWorkspaceIds = userWorkspaces
            .Select(w => w.Id)
            .ToList();

        var userExternalBoxes = GetBoxesAvailableToUser(
            user,
            boxExternalIds,
            connection);

        if (userWorkspaces.Any())
        {
            workspaces = GetMatchingWorkspaces(
                userWorkspaces,
                phrase);
            
            workspaceFolders = SearchFoldersInWorkspaces(
                userWorkspaceIds,
                query,
                connection);

            foreach (var folder in workspaceFolders)
            {
                workspaceGroupExternalIds.Add(folder.WorkspaceExternalId);
            }

            workspaceBoxes = SearchBoxesInWorkspaces(
                userWorkspaceIds,
                query,
                connection);

            foreach (var box in workspaceBoxes)
            {
                workspaceGroupExternalIds.Add(box.WorkspaceExternalId);
            }

            workspaceFiles = SearchFilesInWorkspaces(
                userWorkspaceIds,
                query,
                connection);

            foreach (var file in workspaceFiles)
            {
                workspaceGroupExternalIds.Add(file.WorkspaceExternalId);
            }
        }

        if (userExternalBoxes.Any())
        {
            externalBoxes = GetMatchingExternalBoxes(
                userExternalBoxes,
                phrase);
            
            var readableExternalBoxes = userExternalBoxes
                .Where(b => b is { AllowList: true, HasFolder: true, IsEnabled: true })
                .Select(b => b.Id)
                .ToList();

            externalBoxFolders = SearchFoldersInExternalBoxes(
                readableExternalBoxes,
                query,
                connection);

            foreach (var externalBoxFolder in externalBoxFolders)
            {
                externalBoxGroupExternalIds.Add(externalBoxFolder.BoxExternalId);
            }

            externalBoxFiles = SearchFilesInExternalBoxes(
                user,
                readableExternalBoxes,
                query,
                connection);

            foreach (var externalBoxFile in externalBoxFiles)
            {
                externalBoxGroupExternalIds.Add(externalBoxFile.BoxExternalId);
            }
        }

        if (workspaceGroupExternalIds.Any())
        {
            workspaceGroups = GetWorkspaceGroups(
                userWorkspaces, 
                workspaceGroupExternalIds);
        }

        if (externalBoxGroupExternalIds.Any())
        {
            externalBoxGroups = GetExternalBoxGroups(
                userExternalBoxes, 
                externalBoxGroupExternalIds);
        }

        return new SearchResponseDto
        {
            WorkspaceGroups = workspaceGroups ?? [],
            ExternalBoxGroups = externalBoxGroups ?? [],

            Workspaces = workspaces ?? [],
            WorkspaceFolders = workspaceFolders ?? [],
            WorkspaceFiles = workspaceFiles ?? [],
            WorkspaceBoxes = workspaceBoxes ?? [],

            ExternalBoxes = externalBoxes ?? [],
            ExternalBoxFolders = externalBoxFolders ?? [],
            ExternalBoxFiles = externalBoxFiles ?? []
        };
    }

    private static List<SearchResponseDto.WorkspaceGroup> GetWorkspaceGroups(
        List<Workspace> userWorkspaces, 
        HashSet<string> workspaceGroupExternalIds)
    {
        return userWorkspaces
            .Where(w => workspaceGroupExternalIds.Contains(w.ExternalId))
            .Select(w => new SearchResponseDto.WorkspaceGroup
            {
                ExternalId = w.ExternalId,
                Name = w.Name,
                IsOwnedByUser = w.IsOwnedByUser,
                AllowShare = w.AllowShare
            })
            .ToList();
    }

    private static List<SearchResponseDto.Workspace> GetMatchingWorkspaces(
        List<Workspace> userWorkspaces, 
        string phrase)
    {
        return userWorkspaces
            .Where(w => w.Name.Contains(phrase, StringComparison.InvariantCultureIgnoreCase))
            .Select(w => new SearchResponseDto.Workspace
            {
                ExternalId = w.ExternalId,
                Name = w.Name,
                OwnerEmail = w.OwnerEmail,
                OwnerExternalId = w.OwnerExternalId,
                IsUsedByIntegration = w.IsUsedByIntegration,
                AllowShare = w.AllowShare,
                CurrentSizeInBytes = w.CurrentSizeInBytes,
                MaxSizeInBytes = w.MaxSizeInBytes ?? -1,
                IsBucketCreated = w.IsBucketCreated,
                IsOwnedByUser = w.IsOwnedByUser
            })
            .ToList();
    }

    private static List<SearchResponseDto.ExternalBoxGroup> GetExternalBoxGroups(
        List<ExternalBox> userExternalBoxes, 
        HashSet<string> externalBoxGroupExternalIds)
    {
        return userExternalBoxes
            .Where(b => externalBoxGroupExternalIds.Contains(b.ExternalId))
            .Select(b => new SearchResponseDto.ExternalBoxGroup
            {
                ExternalId = b.ExternalId,
                Name = b.Name,
                AllowCreateFolder = b.AllowCreateFolder,
                AllowDeleteFile = b.AllowDeleteFile,
                AllowRenameFile = b.AllowRenameFile,
                AllowMoveItems = b.AllowMoveItems,
                AllowDeleteFolder = b.AllowDeleteFolder,
                AllowRenameFolder = b.AllowRenameFolder,
                AllowUpload = b.AllowUpload,
                AllowDownload = b.AllowDownload,
                AllowList = b.AllowList
            })
            .ToList();
    }

    private static List<SearchResponseDto.ExternalBox> GetMatchingExternalBoxes(
        List<ExternalBox> userExternalBoxes, 
        string phrase)
    {
        return userExternalBoxes
            .Where(b => b.Name.Contains(phrase, StringComparison.InvariantCultureIgnoreCase))
            .Select(b => new SearchResponseDto.ExternalBox
            {
                ExternalId = b.ExternalId,
                Name = b.Name,
                OwnerEmail = b.OwnerEmail,
                OwnerExternalId = b.OwnerExternalId,
                AllowCreateFolder = b.AllowCreateFolder,
                AllowDeleteFile = b.AllowDeleteFile,
                AllowRenameFile = b.AllowRenameFile,
                AllowMoveItems = b.AllowMoveItems,
                AllowDeleteFolder = b.AllowDeleteFolder,
                AllowRenameFolder = b.AllowRenameFolder,
                AllowUpload = b.AllowUpload,
                AllowDownload = b.AllowDownload,
                AllowList = b.AllowList
            })
            .ToList();
    }

    private static List<SearchResponseDto.ExternalBoxFile> SearchFilesInExternalBoxes(
        UserContext user,
        List<int> userBoxIds,
        string query,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                    SELECT                          
                        fi_external_id,
                        fi_name,
                        bo_external_id,
                        fi_size_in_bytes,
                        fi_extension,
                        (
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
                                AND bo_folder_id IN (
                                    SELECT value FROM json_each(af.fo_ancestor_folder_ids)
                                )
                        ) AS folder_path,
                        (
                            fi_uploader_identity_type = 'user_external_id'
                            AND fi_uploader_identity = $userExternalId
                        ) AS was_uploaded_by_user
                    FROM fi_files
                    INNER JOIN fo_folders AS fo
                        ON fo.fo_id = fi_folder_id
                    INNER JOIN bo_boxes
                        ON bo_folder_id IN (
                            SELECT value FROM json_each(fo.fo_ancestor_folder_ids)
                        ) OR bo_folder_id = fo.fo_id
                    WHERE                        
                        bo_id IN (SELECT value FROM json_each($boxIds))
                        AND fi_name LIKE $query
                    """,
                readRowFunc: reader => new SearchResponseDto.ExternalBoxFile
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1),
                    BoxExternalId = reader.GetString(2),
                    SizeInBytes = reader.GetInt64(3),
                    Extension = reader.GetString(4),
                    FolderPath = reader.GetFromJson<List<SearchResponseDto.FolderAncestor>>(5),
                    WasUploadedByUser = reader.GetBoolean(6)
                })
            .WithParameter("$userExternalId", user.ExternalId.Value)
            .WithJsonParameter("$boxIds", userBoxIds)
            .WithParameter("$query", query)
            .Execute();
    }

    private static List<SearchResponseDto.ExternalBoxFolder> SearchFoldersInExternalBoxes(
        List<int> userBoxIds,
        string query,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                    SELECT
                        fo_external_id,
                        fo_name,
                        bo_external_id,
                        (
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
                                )
                                AND af.fo_is_being_deleted = FALSE
                                AND bo_folder_id IN (
                                    SELECT value FROM json_each(af.fo_ancestor_folder_ids)
                                )
                        ) AS ancestors    
                    FROM fo_folders AS fo
                    INNER JOIN bo_boxes
                        ON bo_folder_id IN (
                            SELECT value FROM json_each(fo.fo_ancestor_folder_ids)
                        )
                    WHERE                        
                        bo_id IN (SELECT value FROM json_each($boxIds))
                        AND fo.fo_is_being_deleted = FALSE
                        AND fo.fo_name LIKE $query
                    """,
                readRowFunc: reader => new SearchResponseDto.ExternalBoxFolder
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1),
                    BoxExternalId = reader.GetString(2),
                    Ancestors = reader.GetFromJson<List<SearchResponseDto.FolderAncestor>>(3)
                })
            .WithJsonParameter("$boxIds", userBoxIds)
            .WithParameter("$query", query)
            .Execute();
    }

    private static List<SearchResponseDto.WorkspaceFile> SearchFilesInWorkspaces(
        List<int> userWorkspaceIds,
        string query,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT                          
                         fi_external_id,
                         fi_name,
                         w_external_id,
                         fi_size_in_bytes,
                         fi_extension,
                         (CASE
                             WHEN fi_folder_id IS NULL THEN json('[]')
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
                         END) AS folder_path
                     FROM fi_files
                     INNER JOIN w_workspaces
                         ON w_id = fi_workspace_id
                     LEFT JOIN fo_folders AS fo
                         ON fo.fo_id = fi_folder_id
                         AND fo.fo_is_being_deleted = FALSE
                     WHERE                        
                         w_id IN (SELECT value FROM json_each($workspaceIds))
                         AND fi_name LIKE $query
                     """,
                readRowFunc: reader => new SearchResponseDto.WorkspaceFile
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1),
                    WorkspaceExternalId = reader.GetString(2),
                    SizeInBytes = reader.GetInt64(3),
                    Extension = reader.GetString(4),
                    FolderPath = reader.GetFromJson<List<SearchResponseDto.FolderAncestor>>(5)
                })
            .WithJsonParameter("$workspaceIds", userWorkspaceIds)
            .WithParameter("$query", query)
            .Execute();
    }

    private static List<SearchResponseDto.WorkspaceBox> SearchBoxesInWorkspaces(
        List<int> userWorkspaceIds,
        string query,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT        
                        bo_external_id,    
                        bo_name,       
                        w_external_id,  
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
                        END) AS folder_path
                     FROM bo_boxes
                     INNER JOIN w_workspaces
                         ON w_id = bo_workspace_id
                     LEFT JOIN fo_folders AS fo
                         ON fo.fo_id = bo_folder_id
                         AND fo.fo_is_being_deleted = FALSE
                     WHERE                        
                         w_id IN (SELECT value FROM json_each($workspaceIds))
                         AND bo_is_being_deleted = FALSE
                         AND bo_name LIKE $query
                     """,
                readRowFunc: reader => new SearchResponseDto.WorkspaceBox
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1),
                    WorkspaceExternalId = reader.GetString(2),
                    IsEnabled = reader.GetBoolean(3),
                    FolderPath = reader.GetFromJson<List<SearchResponseDto.FolderAncestor>>(4)
                })
            .WithJsonParameter("$workspaceIds", userWorkspaceIds)
            .WithParameter("$query", query)
            .Execute();
    }

    private static List<SearchResponseDto.WorkspaceFolder> SearchFoldersInWorkspaces(
        List<int> userWorkspaceIds,
        string query,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT
                        fo_external_id,
                        fo_name,
                        w_external_id,
                        (
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
                                )
                                 AND af.fo_is_being_deleted = FALSE	
                         ) AS ancestors            
                     FROM fo_folders AS fo
                     INNER JOIN w_workspaces
                         ON w_id = fo.fo_workspace_id
                     WHERE                        
                         w_id IN (SELECT value FROM json_each($workspaceIds))
                         AND fo.fo_is_being_deleted = FALSE
                         AND fo.fo_name LIKE $query
                     """,
                readRowFunc: reader => new SearchResponseDto.WorkspaceFolder
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1),
                    WorkspaceExternalId = reader.GetString(2),
                    Ancestors = reader.GetFromJson<List<SearchResponseDto.FolderAncestor>>(3)
                })
            .WithJsonParameter("$workspaceIds", userWorkspaceIds)
            .WithParameter("$query", query)
            .Execute();
    }

    private static List<Workspace> GetWorkspacesAvailableToUser(
        UserContext user,
        string[] workspaceExternalIds,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     WITH user_workspaces AS (
                        SELECT w_id
                        FROM w_workspaces
                        WHERE
                             w_owner_id = $userId
                             AND w_is_being_deleted = FALSE
                             AND (
                                 NOT EXISTS (SELECT 1 FROM json_each($workspaceExternalIds))
                                 OR w_external_id IN (SELECT value FROM json_each($workspaceExternalIds))
                             )
                        UNION ALL
                        SELECT w_id
                        FROM wm_workspace_membership
                        INNER JOIN w_workspaces
                            ON  w_id = wm_workspace_id
                        WHERE
                            wm_member_id = $userId
                            AND wm_was_invitation_accepted = TRUE
                            AND w_is_being_deleted = FALSE
                            AND (
                                 NOT EXISTS (SELECT 1 FROM json_each($workspaceExternalIds))
                                 OR w_external_id IN (SELECT value FROM json_each($workspaceExternalIds))
                            )         
                     )
                     SELECT
                        w_id,
                        w_external_id,
                        w_name,
                        w_current_size_in_bytes, 
                        w_max_size_in_bytes,
                        u_email,
                        u_external_id,
                        (u_id = $userId) AS is_owned_by_user,
                        (CASE
                             WHEN w_owner_id = $userId THEN TRUE
                             ELSE (
                                 SELECT wm_allow_share
                                 FROM wm_workspace_membership
                                 WHERE wm_workspace_id = w_id
                                     AND wm_member_id = $userId
                             )
                         END) AS allow_share,
                         (
                             SELECT EXISTS (       
                                 SELECT 1
                                 FROM i_integrations
                                 WHERE i_workspace_id = w_id
                             )
                         ) AS is_used_by_integration,
                         w_is_bucket_created
                     FROM w_workspaces
                     INNER JOIN u_users
                         ON u_id = w_owner_id
                     WHERE 
                         w_id IN (SELECT w_id FROM user_workspaces)
                     """,
                readRowFunc: reader => new Workspace
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetString(1),
                    Name = reader.GetString(2),
                    CurrentSizeInBytes = reader.GetInt64(3),
                    MaxSizeInBytes = reader.GetInt64OrNull(4),
                    OwnerEmail = reader.GetString(5),
                    OwnerExternalId = reader.GetString(6),
                    IsOwnedByUser = reader.GetBoolean(7),
                    AllowShare = reader.GetBoolean(8),
                    IsUsedByIntegration = reader.GetBoolean(9),
                    IsBucketCreated = reader.GetBoolean(10)
                })
            .WithParameter("$userId", user.Id)
            .WithJsonParameter("$workspaceExternalIds", workspaceExternalIds)
            .Execute();
    }

    private static List<ExternalBox> GetBoxesAvailableToUser(
        UserContext user,
        string[] boxExternalIds,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT 
                         bo_id,
                         bo_external_id,
                         bo_is_enabled,
                         bo_folder_id IS NOT NULL,
                         bo_name,
                         
                         u_email,
                         u_external_id,
                         
                         bm_allow_download, 
                         bm_allow_upload, 
                         bm_allow_list, 
                         bm_allow_delete_file, 
                         bm_allow_rename_file, 
                         bm_allow_move_items, 
                         bm_allow_create_folder, 
                         bm_allow_delete_folder, 
                         bm_allow_rename_folder
                     FROM bm_box_membership
                     INNER JOIN bo_boxes
                         ON bo_id = bm_box_id 
                     INNER JOIN w_workspaces
                         ON w_id = bo_workspace_id
                     INNER JOIN u_users
                         ON u_id = w_owner_id
                     WHERE
                         bm_member_id = $userId
                         AND bm_was_invitation_accepted = TRUE
                         AND bo_is_being_deleted = FALSE
                         AND (
                             NOT EXISTS (SELECT 1 FROM json_each($boxExternalIds))
                             OR bo_external_id IN (SELECT value FROM json_each($boxExternalIds))
                         )                       
                     """,
                readRowFunc: reader => new ExternalBox
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetString(1),
                    IsEnabled = reader.GetBoolean(2),
                    HasFolder = reader.GetBoolean(3),
                    Name = reader.GetString(4),
                    OwnerEmail = reader.GetString(5),
                    OwnerExternalId = reader.GetString(6),

                    AllowDownload = reader.GetBoolean(7),
                    AllowUpload = reader.GetBoolean(8),
                    AllowList = reader.GetBoolean(9),
                    AllowDeleteFile = reader.GetBoolean(10),
                    AllowRenameFile = reader.GetBoolean(11),
                    AllowMoveItems = reader.GetBoolean(12),
                    AllowCreateFolder = reader.GetBoolean(13),
                    AllowDeleteFolder = reader.GetBoolean(14),
                    AllowRenameFolder = reader.GetBoolean(15)
                })
            .WithParameter("$userId", user.Id)
            .WithJsonParameter("$boxExternalIds", boxExternalIds)
            .Execute();
    }

    private class Workspace
    {
        public required int Id { get; init; }
        public required string ExternalId { get; init; }

        public required string Name { get; init; }
        public required long CurrentSizeInBytes { get; init; }
        public required long? MaxSizeInBytes { get; init; }
        public required string OwnerEmail { get; init; }
        public required string OwnerExternalId { get; init; }
        public required bool IsOwnedByUser { get; init; }
        public required bool AllowShare { get; init; }
        public required bool IsUsedByIntegration { get; init; }
        public required bool IsBucketCreated { get; init; }
    }

    private class ExternalBox
    {
        public required int Id { get; init; }
        public required string ExternalId { get; init; }

        public required bool IsEnabled { get; init; }
        public required bool HasFolder { get; init; }
        public required string Name { get; init; }
        public required string OwnerEmail { get; init; }
        public required string OwnerExternalId { get; init; }

        public required bool AllowDownload { get; init; }
        public required bool AllowUpload { get; init; }
        public required bool AllowList { get; init; }
        public required bool AllowDeleteFile { get; init; }
        public required bool AllowRenameFile { get; init; }
        public required bool AllowMoveItems { get; init; }
        public required bool AllowCreateFolder { get; init; }
        public required bool AllowDeleteFolder { get; init; }
        public required bool AllowRenameFolder { get; init; }
    }
}