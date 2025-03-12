using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Folders.Create;

public class GetOrCreateFolderQuery(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public async Task<BulkResult> Execute(
        WorkspaceContext workspace,
        FolderExtId? parentFolderExternalId,
        int? boxFolderId,
        IUserIdentity userIdentity,
        List<FolderTreeDto> folderTreeItems, 
        bool ensureUniqueNames,
        CancellationToken cancellationToken = default)
    {
        var duplicatedTemporaryIds = CheckForDuplicatedTemporaryIds(
            folderTreeItems: folderTreeItems);

        if (duplicatedTemporaryIds.Count > 0)
            return new BulkResult(
                Code: ResultCode.DuplicatedTemporaryIds,
                TemporaryIdsWithDuplications: duplicatedTemporaryIds);

        var duplicatedNames = CheckForDuplicatedNames(
            folderTreeItems: folderTreeItems);

        if (duplicatedNames.Count > 0)
            return new BulkResult(
                Code: ResultCode.DuplicatedNamesFound,
                TemporaryIdsWithDuplications: duplicatedNames);

        var (code, foldersToCreate, existingFolders) = CheckParentFolderAndGetFoldersToCreate(
            workspace: workspace, 
            parentFolderExternalId: parentFolderExternalId, 
            boxFolderId: boxFolderId, 
            folderTreeItems: folderTreeItems, 
            ensureUniqueNames: ensureUniqueNames);

        if (code == ResultCode.ParentFolderNotFound)
            return new BulkResult(Code: ResultCode.ParentFolderNotFound);

        var createdFolders = foldersToCreate.Count > 0
            ? await CreateMissingFolders(
                workspace: workspace,
                userIdentity: userIdentity,
                inputTreeNodes: foldersToCreate,
                cancellationToken: cancellationToken)
            : [];

        existingFolders.AddRange(createdFolders);

        return new BulkResult(
            Code: ResultCode.Ok,
            Response: new BulkCreateFolderResponseDto
            {
                Items = existingFolders
            });
    }

    private List<int> CheckForDuplicatedTemporaryIds(
        List<FolderTreeDto> folderTreeItems)
    {
        var stack = new Stack<List<FolderTreeDto>>();
        var ids = new HashSet<int>();
        var duplicatedIds = new List<int>();

        stack.Push(folderTreeItems);

        while (stack.Any())
        {
            var currentItems = stack.Pop();

            foreach (var currentItem in currentItems)
            {
                if (!ids.Add(currentItem.TemporaryId))
                {
                    duplicatedIds.Add(currentItem.TemporaryId);
                }

                if(currentItem.Subfolders is not null)
                    stack.Push(currentItem.Subfolders);
            }
        }

        return duplicatedIds;
    }

    private List<int> CheckForDuplicatedNames(
        List<FolderTreeDto> folderTreeItems)
    {
        var stack = new Stack<List<FolderTreeDto>>();

        stack.Push(folderTreeItems);

        while (stack.Any())
        {
            var currentItems = stack.Pop();

            var duplications = currentItems
                .GroupBy(x => x.Name)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.Select(x => x.TemporaryId))
                .ToList();

            if (duplications.Any())
                return duplications;

            foreach (var currentItem in currentItems)
            {
                if(currentItem.Subfolders is not null)
                    stack.Push(currentItem.Subfolders);
            }
        }

        return [];
    }

    private (ResultCode Code, List<FolderTreeNode> InputTreeNodes, List<BulkCreateFolderItemDto> ExistingFolders) CheckParentFolderAndGetFoldersToCreate(
        WorkspaceContext workspace,
        FolderExtId? parentFolderExternalId,
        int? boxFolderId,
        List<FolderTreeDto> folderTreeItems,
        bool ensureUniqueNames)
    {
        if(parentFolderExternalId is null && boxFolderId is not null)
            throw new InvalidOperationException(
                $"Top folders cannot be created from within the box with boxFolderId is not null ({boxFolderId})"); ;

        if (parentFolderExternalId is null && !ensureUniqueNames)
            return (
                Code: ResultCode.Ok,
                InputTreeNodes:
                [
                    new FolderTreeNode
                    {
                        Parent = null,
                        Subfolders = folderTreeItems
                    }
                ],
                ExistingFolders: []);

        using var connection = plikShareDb.OpenConnection();

        Folder? parentFolder = null;

        if (parentFolderExternalId is not null)
        {
            var parentFolderResult = TryGetParentFolder(
                workspace: workspace,
                parentFolderExternalId: parentFolderExternalId.Value,
                boxFolderId: boxFolderId,
                connection: connection);

            if (parentFolderResult.IsEmpty)
                return (
                    Code: ResultCode.ParentFolderNotFound,
                    InputTreeNodes: [],
                    ExistingFolders: []
                );

            parentFolder = parentFolderResult.Value;
        }

        if (!ensureUniqueNames)
            return (
                Code: ResultCode.Ok,
                InputTreeNodes: [
                    new FolderTreeNode
                    {
                        Parent = parentFolder,
                        Subfolders = folderTreeItems
                    }
                ],
                ExistingFolders: []
            );
        
        var existingFoldersResult = TryGetExistingFoldersFast(
            workspace: workspace,
            parent: parentFolder,
            folderTreeItems: folderTreeItems,
            connection: connection);
        
        var inputTreeNodes = GetMissingFoldersList(
            existingFolders: existingFoldersResult,
            parent: parentFolder,
            folderTreeItems: folderTreeItems);

        var existingFolders = existingFoldersResult
            .Select(kvp => new BulkCreateFolderItemDto
            {
                TemporaryId = kvp.Key,
                ExternalId = kvp.Value.ExternalId
            })
            .ToList();

        return (
            Code: ResultCode.Ok,
            InputTreeNodes: inputTreeNodes,
            ExistingFolders: existingFolders
        );
    }

    private Dictionary<int, Folder> TryGetExistingFoldersFast(
        WorkspaceContext workspace,
        Folder? parent,
        List<FolderTreeDto> folderTreeItems,
        SqliteConnection connection)
    {
        using var commandsPool = connection.CreateLazyCommandsPool();

        var stack = new Stack<List<FolderTreeNode>>();
        var results = new Dictionary<int, Folder>();

        stack.Push([new FolderTreeNode
        {
            Parent = parent,
            Subfolders = folderTreeItems
        }]);

        while (stack.Any())
        {
            var nextLevelItems = new List<FolderTreeNode>();
            var folderTreeNodes = stack.Pop();

            var existingFolders = folderTreeNodes.Count == 1 && folderTreeNodes[0].Parent is null
                ? GetExistingTopFolders(
                    commandsPool,
                    workspace)
                : GetExistingFolders(
                    commandsPool,
                    workspace,
                    folderTreeNodes.Select(x => x.Parent!.Id).ToList());

            foreach (var folderTreeNode in folderTreeNodes)
            {
                var existingFoldersForParent = existingFolders
                    .Where(ef => ef.ParentId == folderTreeNode.Parent?.Id)
                    .ToList();

                foreach (var subfolder in folderTreeNode.Subfolders)
                {
                    var existingFolder = existingFoldersForParent
                        .FirstOrDefault(ef => ef.Name == subfolder.Name);

                    if (existingFolder is not null)
                    {
                        results.Add(
                            key: subfolder.TemporaryId,
                            value: existingFolder);

                        nextLevelItems.Add(new FolderTreeNode
                        {
                            Parent = existingFolder,
                            Subfolders = subfolder.Subfolders ?? []
                        });
                    }
                }
            }

            if (nextLevelItems.Any())
            {
                stack.Push(nextLevelItems);
            }
        }

        return results;
    }

    private List<FolderTreeNode> GetMissingFoldersList(
        Dictionary<int, Folder> existingFolders,
        Folder? parent,
        List<FolderTreeDto> folderTreeItems)
    {
        var result = new List<FolderTreeNode>();
        var stack = new Stack<FolderTreeNode>();

        stack.Push(new FolderTreeNode
        {
            Parent = parent,
            Subfolders = folderTreeItems
        });

        while (stack.Any())
        {
            var folderTreeNode = stack.Pop();

            var nonExistingSubfolders = new List<FolderTreeDto>();

            for (var i = 0; i < folderTreeNode.Subfolders.Count; i++)
            {
                var subfolder = folderTreeNode.Subfolders[i];

                var doesExist = existingFolders.ContainsKey(subfolder.TemporaryId);

                if (!doesExist)
                {
                    nonExistingSubfolders.Add(subfolder);
                }
                else
                {
                    var existingFolder = existingFolders[subfolder.TemporaryId];

                    stack.Push(new FolderTreeNode
                    {
                        Parent = existingFolder,
                        Subfolders = subfolder.Subfolders ?? []
                    });
                }
            }

            if (nonExistingSubfolders.Count > 0)
            {
                result.Add(new FolderTreeNode
                {
                    Parent = folderTreeNode.Parent,
                    Subfolders = nonExistingSubfolders
                });
            }
        }

        return result;
    }

    private async Task<List<BulkCreateFolderItemDto>> CreateMissingFolders(
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        List<FolderTreeNode> inputTreeNodes,
        CancellationToken cancellationToken)
    {
        var results = new List<BulkCreateFolderItemDto>();
        var stack = new Stack<List<FolderTreeNode>>();

        stack.Push(inputTreeNodes);
       
        var foldersToCreate = new List<FolderToCreate>();
        var subfoldersDataList = new List<(FolderTreeNode Node, FolderTreeDto Subfolder, FolderExtId ExternalId)>();

        while (stack.Any())
        {
            foldersToCreate.Clear();
            subfoldersDataList.Clear();

            var nextLevelItems = new List<FolderTreeNode>();
            var currentTreeLevel = stack.Pop();

            for (var i = 0; i < currentTreeLevel.Count; i++)
            {
                var node = currentTreeLevel[i];

                for (var j = 0; j < node.Subfolders.Count; j++)
                {
                    var subfolder = node.Subfolders[j];

                    var externalId = FolderExtId.NewId();

                    subfoldersDataList.Add((node, subfolder, externalId));

                    foldersToCreate.Add(new FolderToCreate
                    {
                        ExternalId = externalId.Value,
                        AncestorFolderIds = node.Parent?.AncestorFolderIdsForSubfolders ?? [],
                        Name = subfolder.Name,
                        ParentId = node.Parent?.Id
                    });
                }
            }

            if (foldersToCreate.Count > 0)
            {
                var createdFolders = await dbWriteQueue.Execute(
                    operationToEnqueue: context => CreateFolders(
                        dbWriteContext: context,
                        workspace: workspace,
                        foldersToCreate: foldersToCreate,
                        userIdentity: userIdentity),
                    cancellationToken: cancellationToken);

                for (var index = 0; index < subfoldersDataList.Count; index++)
                {
                    var subfolderData = subfoldersDataList[index];

                    var (node, subfolder, externalId) = subfolderData;
                    var createdFolder = createdFolders[subfolderData.ExternalId.Value];

                    results.Add(new BulkCreateFolderItemDto
                    {
                        TemporaryId = subfolder.TemporaryId,
                        ExternalId = externalId.Value,
                    });

                    var folder = new Folder
                    {
                        ExternalId = externalId.Value,
                        Id = createdFolder.Id,
                        ParentId = node.Parent?.Id,
                        Name = subfolder.Name,

                        AncestorFolderIdsForSubfolders =
                        [
                            ..node.Parent?.AncestorFolderIdsForSubfolders ?? [], createdFolder.Id
                        ]
                    };

                    if (subfolder.Subfolders is not null && subfolder.Subfolders.Count > 0)
                    {
                        nextLevelItems.Add(new FolderTreeNode
                        {
                            Parent = folder,
                            Subfolders = subfolder.Subfolders
                        });
                    }
                }
            }
            
            if (nextLevelItems.Count > 0)
            {
                stack.Push(nextLevelItems);
            }
        }

        return results;
    }
    
    private List<Folder> GetExistingTopFolders(
        LazySqLiteCommandsPool commandsPool,
        WorkspaceContext workspace)
    {
        var result = commandsPool
            .Cmd(
                sql: @"
                SELECT 
                    fo_id,
                    fo_external_id,
                    fo_name
                FROM fo_folders AS fo
                WHERE 
                    fo_workspace_id = $workspaceId
                    AND fo_is_being_deleted = FALSE
                    AND fo_parent_folder_id IS NULL
            ",
                readRowFunc: reader =>
                {
                    var id = reader.GetInt32(0);

                    return new Folder
                    {
                        Id = id,
                        ParentId = null,
                        ExternalId = reader.GetString(1),
                        Name = reader.GetString(2),
                        AncestorFolderIdsForSubfolders = [id]
                    };
                })
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        return result;
    }

    private List<Folder> GetExistingFolders(
        LazySqLiteCommandsPool commandsPool,
        WorkspaceContext workspace,
        List<int> parentIds)
    {
        var result = commandsPool
            .Cmd(
                sql: """
                     SELECT 
                         fo_id,
                         fo_external_id,
                         fo_name,
                         fo_ancestor_folder_ids
                     FROM fo_folders AS fo
                     WHERE 
                         fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                         AND fo_parent_folder_id IN (
                             SELECT value FROM json_each($parentIds)
                         )
                     """,
                readRowFunc: reader =>
                {
                    var id = reader.GetInt32(0);
                    var externalId = reader.GetString(1);
                    var name = reader.GetString(2);
                    var ancestorFolderIds = reader.GetFromJson<int[]>(3);
                    var parentId = ancestorFolderIds.Last();
                    
                    return new Folder
                    {
                        Id = id,
                        ParentId = parentId,
                        ExternalId = externalId,
                        Name = name, 
                        AncestorFolderIdsForSubfolders = [.. ancestorFolderIds, id]
                    };
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$parentIds", parentIds)
            .Execute();

        return result;
    }

    private Dictionary<string, CreatedFolder> CreateFolders(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        List<FolderToCreate> foldersToCreate,
        IUserIdentity userIdentity)
    {
        var result = dbWriteContext
            .Cmd(
                sql: """
                     INSERT INTO fo_folders (
                         fo_external_id,
                         fo_workspace_id,
                         fo_parent_folder_id,
                         fo_ancestor_folder_ids,
                         fo_name,
                         fo_is_being_deleted,
                         fo_creator_identity_type,
                         fo_creator_identity,
                         fo_created_at    
                     ) 
                     SELECT 
                         json_extract(value, '$.externalId'),
                         $workspaceId,
                         json_extract(value, '$.parentId'),
                         json_extract(value, '$.ancestorFolderIds'),
                         json_extract(value, '$.name'),
                         0,
                         $creatorIdentityType,
                         $creatorIdentity,
                         $createdAt
                     FROM json_each($foldersToCreate)
                     RETURNING 
                         fo_id,
                         fo_external_id
                     """,
                readRowFunc: reader => new CreatedFolder
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetString(1)
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
            .WithParameter("$creatorIdentity", userIdentity.Identity)
            .WithParameter("$createdAt", clock.UtcNow)
            .WithJsonParameter("$foldersToCreate", foldersToCreate)
            .Execute();

        return result.ToDictionary(
            keySelector: cf => cf.ExternalId,
            elementSelector: cf => cf);
    }
    
    private SQLiteOneRowCommandResult<Folder> TryGetParentFolder(
        WorkspaceContext workspace,
        FolderExtId parentFolderExternalId,
        int? boxFolderId,
        SqliteConnection connection)
    {
        return connection
            .OneRowCmd(
                sql: """
                     SELECT 
                         fo_id, 
                         fo_parent_folder_id,
                         fo_ancestor_folder_ids,
                         fo_name
                     FROM 
                         fo_folders
                     WHERE 
                         fo_external_id = $parentExternalId
                         AND fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                         AND (
                             $boxFolderId IS NULL 
                             OR $boxFolderId = fo_id 
                             OR $boxFolderId IN (
                                 SELECT value FROM json_each(fo_ancestor_folder_ids) 
                             ) 
                         )
                     LIMIT 1
                     """,
                readRowFunc: reader =>
                {
                    var id = reader.GetInt32(0);
                    var parentId = reader.GetInt32OrNull(1);
                    var ancestorFolderIds = reader.GetFromJson<int[]>(2);

                    return new Folder
                    {
                        Id = id,
                        ParentId = parentId,
                        AncestorFolderIdsForSubfolders = [..ancestorFolderIds, id],
                        Name = reader.GetString(2),
                        ExternalId = parentFolderExternalId.Value
                    };
                })
            .WithParameter("$parentExternalId", parentFolderExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();
    }
    
    public enum ResultCode
    {
        Ok = 0,
        ParentFolderNotFound,
        DuplicatedNamesFound,
        DuplicatedTemporaryIds
    }

    public readonly record struct BulkResult(
        ResultCode Code,
        BulkCreateFolderResponseDto? Response = default,
        List<int>? TemporaryIdsWithDuplications = default);

    private class Folder
    {
        public required int Id { get; init; }
        public required int? ParentId { get; init; }
        public required string ExternalId { get; init; }
        public required string Name { get; init; }

        //this field is folder AncestorFoldersIds + Id - so how ancestor folders ids will look like for its children
        public required int[] AncestorFolderIdsForSubfolders { get; init; }
    }

    public class FolderToCreate
    {
        public required int? ParentId { get; init; }
        public required int[] AncestorFolderIds { get; init; }
        public required string Name { get; init; }
        public required string ExternalId { get; init; }
    }

    private class CreatedFolder
    {
        public required int Id { get; init; }
        public required string ExternalId { get; init; }
    }

    private class FolderTreeNode
    {
        public required Folder? Parent { get; init; }
        public required List<FolderTreeDto> Subfolders { get; init; }
    }
}

