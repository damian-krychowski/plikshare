using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.SearchFilesTree.Contracts;

namespace PlikShare.Workspaces.SearchFilesTree;

public class SearchFilesTreeQuery(PlikShareDb plikShareDb)
{
    public const int TooManyResultsThreshold = 1000;

    public SearchFilesTreeResponseDto Execute(
        WorkspaceContext workspace,
        SearchFilesTreeRequestDto request,
        IUserIdentity userIdentity,
        int? boxFolderId)
    {
        using var connection = plikShareDb.OpenConnection();

        var doesParentFolderExist = TryGetParentFolderId(
            connection,
            workspace.Id,
            request.FolderExternalId,
            userIdentity,
            boxFolderId,
            out var parentFolderId);

        if (!doesParentFolderExist)
        {
            return new SearchFilesTreeResponseDto
            {
                Files = [],
                Folders = [],
                FolderExternalIds = [],
                TooManyResultsCounter = -1
            };
        }

        var matchingFiles = GetMatchingFiles(
            workspaceId: workspace.Id,
            parentFolderId: parentFolderId,
            phrase: request.Phrase,
            userIdentity,
            connection);

        if (matchingFiles.Count > TooManyResultsThreshold)
        {
            return new SearchFilesTreeResponseDto
            {
                Files = [],
                Folders = [],
                FolderExternalIds = [],
                TooManyResultsCounter = matchingFiles.Count
            };
        }

        var folderIds = matchingFiles
            .Where(fi => fi.FolderId.HasValue)
            .Select(fi => fi.FolderId!.Value)
            .Distinct()
            .ToList();

        var allFolders = GetFoldersAndAncestors(
            folderIds: folderIds,
            parentFolderId: parentFolderId,
            userIdentity: userIdentity,
            connection: connection);

        var response = BuildResponse(
            allFolders: allFolders, 
            matchingFiles: matchingFiles);

        return response;
    }

    private static SearchFilesTreeResponseDto BuildResponse(
        List<Folder> allFolders, 
        List<File> matchingFiles)
    {
        var response = new SearchFilesTreeResponseDto
        {
            FolderExternalIds = [],
            Files = [],
            Folders = [],
            TooManyResultsCounter = -1
        };

        var folderIdFolderIndexMap = new Dictionary<int, int>();

        for (var i = 0; i < allFolders.Count; i++)
        {
            var folder = allFolders[i];
            folderIdFolderIndexMap.Add(folder.Id, i);
            response.FolderExternalIds.Add(folder.ExternalId);
        }

        for (var i = 0; i < allFolders.Count; i++)
        {
            var folder = allFolders[i];
            var idIndex = folderIdFolderIndexMap[folder.Id];
            var parentIdIndex = GetParentIdIndex(
                parentId: folder.ParentId, 
                folderIdFolderIndexMap: folderIdFolderIndexMap);

            response.Folders.Add(new SearchFilesTreeFolderItemDto
            {
                IdIndex = idIndex,
                ParentIdIndex = parentIdIndex ?? -1,
                Name = folder.Name,
                WasCreatedByUser = folder.WasCreatedByUser,
                CreatedAt = folder.CreatedAt?.DateTime,
            });
        }

        for (var i = 0; i < matchingFiles.Count; i++)
        {
            var file = matchingFiles[i];
            var folderIdIndex = GetParentIdIndex(
                parentId: file.FolderId,
                folderIdFolderIndexMap: folderIdFolderIndexMap);

            response.Files.Add(new SearchFilesTreeFileItemDto
            {
                ExternalId = file.ExternalId,
                Name = file.Name,
                Extension = file.Extension,
                IsLocked = file.IsLocked,
                SizeInBytes = file.SizeInBytes,
                WasUploadedByUser = file.WasUploadedByUser,
                FolderIdIndex = folderIdIndex ?? -1
            });
        }

        return response;
    }

    private static int? GetParentIdIndex(
        int? parentId, 
        Dictionary<int, int> folderIdFolderIndexMap)
    {
        if (parentId is null)
            return null;

        if (folderIdFolderIndexMap.TryGetValue(parentId.Value, out var index))
            return index;

        return null;
    }

    private bool TryGetParentFolderId(
        SqliteConnection connection, 
        int workspaceId,
        FolderExtId? externalId,
        IUserIdentity userIdentity,
        int? boxFolderId,
        out int? parentFolder)
    {
        if (externalId is null)
        {
            //if box is not provided then we have top folder scenario (parentFolder = null)
            //otherwise we have box top folder scenario (parentFolder = boxFolderId.Value)
            parentFolder = boxFolderId;
            return true;
        }

        var folder = connection
            .OneRowCmd(
                sql: @"
                    SELECT fo_id	            	
			        FROM fo_folders
			        WHERE
                        fo_workspace_id = $workspaceId
			            AND fo_external_id = $parentFolderExternalId
			            AND fo_is_being_deleted = FALSE
                        AND (
                            $boxFolderId IS NULL
                            OR $boxFolderId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                            )
                        )
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$parentFolderExternalId", externalId.Value.Value)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
            .WithParameter("$creatorIdentity", userIdentity.Identity)
            .Execute();

        if (folder.IsEmpty)
        {
            parentFolder = null;
            return false;
        }

        parentFolder = folder.Value;
        return true;
    }

    private List<File> GetMatchingFiles(
        int workspaceId,
        int? parentFolderId,
        string phrase,
        IUserIdentity userIdentity,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: @"
                    SELECT
				        fi_external_id,
                        fi_folder_id,
				        fi_name,
				        fi_extension,
				        fi_size_in_bytes,
						(
							fi_uploader_identity_type = $uploaderIdentityType 
							AND fi_uploader_identity =  $uploaderIdentity
						) AS fi_was_uploaded_by_user,
                        NOT fi_is_upload_completed 
				    FROM fi_files
                    LEFT JOIN fo_folders
                        ON fo_id = fi_folder_id
				    WHERE
				        fi_workspace_id = $workspaceId
                        AND fi_parent_file_id IS NULL
                        AND (fi_name || fi_extension) LIKE $query
                        AND (
                            fi_folder_id IS NULL 
                            OR fo_is_being_deleted = FALSE
                        )
                        AND (
                            $parentFolderId IS NULL
                            OR (
                                fi_folder_id IS NOT NULL
                                AND (
                                    $parentFolderId = fo_id
                                    OR $parentFolderId IN (
                                        SELECT value FROM json_each(fo_ancestor_folder_ids)
                                    )
                                )                                
                            )
                        )
					 ORDER BY
						fi_id DESC
                ",
                readRowFunc: reader => new File
                {
                    ExternalId = reader.GetString(0),
                    FolderId = reader.GetInt32OrNull(1),
                    Name = reader.GetString(2),
                    Extension = reader.GetString(3),
                    SizeInBytes = reader.GetInt64(4),
                    WasUploadedByUser = reader.GetBoolean(5),
                    IsLocked = reader.GetBoolean(6)
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .WithParameter("$query", $"%{phrase}%")
            .WithParameter("$parentFolderId", parentFolderId)
            .Execute();
    }

    private List<Folder> GetFoldersAndAncestors(
        List<int> folderIds,
        int? parentFolderId,
        IUserIdentity userIdentity,
        SqliteConnection connection)
    {

        return connection
            .Cmd(
                sql: @"
                    WITH all_folder_ids AS (
                        SELECT value AS fo_id
                        FROM json_each($folderIds)
                        
                        UNION
    
                        SELECT
                            ancestor.value AS fo_id
                        FROM fo_folders, json_each(fo_ancestor_folder_ids) AS ancestor
                        WHERE fo_id IN (
                            SELECT value FROM json_each($folderIds)
                        )
                    )
                    SELECT 
                        fo_id,
                        fo_external_id,
                        fo_parent_folder_id,
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
                        fo_id IN (
                            SELECT fo_id FROM all_folder_ids
                        )
                        AND (
                            $parentFolderId IS NULL
                            OR $parentFolderId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                            )
                        )
                    ORDER BY fo_id
                ",
                readRowFunc: reader => new Folder
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetString(1),
                    ParentId = reader.GetInt32OrNull(2),
                    Name = reader.GetString(3),
                    WasCreatedByUser = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTimeOffsetOrNull(5)
                })
            .WithJsonParameter("$folderIds", folderIds)
            .WithParameter("$parentFolderId", parentFolderId)
            .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
            .WithParameter("$creatorIdentity", userIdentity.Identity)
            .Execute();
    }
    
    private class Folder
    {
        public required int Id { get; init; }
        public required string ExternalId { get; init; }
        public required int? ParentId { get; init; }
        public required string Name { get; init; }
        public required bool WasCreatedByUser { get; init; }
        public required DateTimeOffset? CreatedAt { get; init; }
    }

    public class File
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Extension { get; init; }
        public required long SizeInBytes { get; init; }
        public required bool IsLocked { get; init; }
        public required bool WasUploadedByUser { get; init; }
        public required int? FolderId { get; init; }
    }
}