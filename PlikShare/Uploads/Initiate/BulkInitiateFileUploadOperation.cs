using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Uploads.Initiate;

public class BulkInitiateFileUploadOperation(
    PlikShareDb plikShareDb,
    FileUploadCache fileUploadCache,
    PreSignedUrlsService preSignedUrlsService,
    BulkInsertFileUploadQuery bulkInsertFileUploadQuery,
    WorkspaceSizeCache workspaceSizeCache,
    IMasterDataEncryption masterDataEncryption,
    IClock clock)
{
    /// <summary>
    /// This function is one of the hot paths of the system.
    /// It is crucial to make it as fast as possible so that users could seamlessly upload lots of files into their PlikShare
    /// </summary>
    public async ValueTask<Result> Execute(
        WorkspaceContext workspace,
        BulkInitiateFileUploadItemDto[] fileDetailsList,
        IUserIdentity userIdentity,
        int? boxFolderId,
        int? boxLinkId,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken = default)
    {
        var workspaceSpace = CheckWorkspaceSpace(
            workspace: workspace,
            fileDetailsList: fileDetailsList);

        if (!workspaceSpace.IsAvailable)
            return new Result(Code: ResultCode.NotEnoughSpace);

        var (folderValidationResultCode, folderValidationResults) = ValidateFolders(
            workspace: workspace,
            fileDetailsList: fileDetailsList,
            boxFolderId: boxFolderId);

        if (folderValidationResultCode == FoldersValidationResultCode.TopFolderUnauthorizedAccess)
        {
            Log.Warning(
                "Could not initiate FileUpload to Top Folder because it is forbidden to do from within a box '{BoxId}'.",
                boxFolderId);

            return new Result(
                Code: ResultCode.TopFolderUnauthorizedAccess);
        }

        if (folderValidationResultCode == FoldersValidationResultCode.FoldersNotFound)
        {
            var missingFolders = folderValidationResults
                .Values
                .Where(r => r.Code == FolderValidationResultCode.NotFound)
                .Select(r => r.FolderExternalId)
                .ToList();

            Log.Warning(
                "Could not initiate FileUploads because some Folders were not found: '{MissingFolderExternalIds}'.",
                missingFolders);

            return new Result(
                Code: ResultCode.FoldersNotFound,
                MissingFolders: missingFolders);
        }

        var batchUploadResults = await HandleMultipleUploads(
            fileDetailsList: fileDetailsList,
            workspace,
            workspaceEncryptionSession,
            cancellationToken);

        var insertFileUploadsResult = await InsertFileUploadsIntoDb(
            workspace,
            userIdentity,
            batchUploadResults,
            folderValidationResults,
            cancellationToken);

        if (insertFileUploadsResult.Code == BulkInsertFileUploadQuery.ResultCode.FolderNotFound)
        {
            await AbortStartedMultiPartUploads(
                workspace,
                batchUploadResults,
                cancellationToken);

            return new Result(
                Code: ResultCode.FoldersNotFound);
        }

        if (insertFileUploadsResult.Code != BulkInsertFileUploadQuery.ResultCode.Ok)
            throw new InvalidOperationException($"Unknown result code: {insertFileUploadsResult.Code}");

        await PreInitializeUploadsCache(
            workspace,
            userIdentity,
            batchUploadResults,
            insertFileUploadsResult,
            folderValidationResults,
            cancellationToken);

        var response = PrepareResponse(
            workspace: workspace,
            boxLinkId: boxLinkId,
            userIdentity: userIdentity,
            batchUploadResults: batchUploadResults,
            newWorkspaceSizeInBytes: boxFolderId is not null
                ? null  //not to reveal size of the workspace to unauthorized users of a box
                : workspaceSpace.NewWorkspaceSizeInBytes,
            workspaceEncryptionSession: workspaceEncryptionSession);

        var initiatedFiles = batchUploadResults
            .Select(bu => new InitiatedFile(
                FileExternalId: bu.S3Key.FileExternalId,
                FileUploadExternalId: new FileUploadExtId(bu.FileUploadExternalId),
                FileName: bu.Name,
                FileExtension: bu.Extension,
                SizeInBytes: bu.SizeInBytes,
                FolderPath: bu.FolderExternalId is not null
                    && folderValidationResults.TryGetValue(bu.FolderExternalId, out var fvr)
                    ? fvr.FolderAncestors?.ToFolderPath()
                    : null))
            .ToList();

        return new Result(
            Code: ResultCode.Ok,
            Response: response,
            InitiatedFiles: initiatedFiles);
    }

    private WorkspaceSpace CheckWorkspaceSpace(
        WorkspaceContext workspace,
        BulkInitiateFileUploadItemDto[] fileDetailsList)
    {
        var newFilesSizeInBytes = fileDetailsList
            .Aggregate(0L, (requiredSpace, file) => requiredSpace + file.FileSizeInBytes);

        var workspaceTotalSizeInBytes = workspaceSizeCache.Get(
            workspace.Id);

        var newWorkspaceSizeInBytes = workspaceTotalSizeInBytes + newFilesSizeInBytes;

        return new WorkspaceSpace(
            IsAvailable: workspace.MaxSizeInBytes is null
                || newWorkspaceSizeInBytes <= workspace.MaxSizeInBytes,
            NewWorkspaceSizeInBytes: newWorkspaceSizeInBytes);
    }


    private BulkInitiateFileUploadResponseDto PrepareResponse(
        WorkspaceContext workspace,
        int? boxLinkId,
        IUserIdentity userIdentity,
        List<UploadDetails> batchUploadResults,
        long? newWorkspaceSizeInBytes,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var directUploadsCount = 0;
        var singleChunkUploads = new List<BulkInitiateSingleChunkUploadResponseDto>();
        var multiStepChunkUploads = new List<BulkInitiateMultiStepChunkUploadResponseDto>();

        for (var i = 0; i < batchUploadResults.Count; i++)
        {
            var upload = batchUploadResults[i];
            var algorithm = upload.StorageUploadDetails.Algorithm;

            if (algorithm == UploadAlgorithm.DirectUpload)
            {
                directUploadsCount++;
            }
            else if (algorithm == UploadAlgorithm.MultiStepChunkUpload)
            {
                multiStepChunkUploads.Add(new BulkInitiateMultiStepChunkUploadResponseDto
                {
                    FileUploadExternalId = upload.FileUploadExternalId,
                    ExpectedPartsCount = upload.StorageUploadDetails.FilePartsCount
                });
            }
            else if (algorithm == UploadAlgorithm.SingleChunkUpload)
            {
                singleChunkUploads.Add(new BulkInitiateSingleChunkUploadResponseDto
                {
                    FileUploadExternalId = upload.FileUploadExternalId,
                    PreSignedUploadLink = upload.StorageUploadDetails.PreSignedUploadLink!,
                    PreSignedUploadLinkRequiredHeaders = upload.StorageUploadDetails.PreSignedUploadLinkRequiredHeaders
                });
            }
            else
            {
                throw new InvalidOperationException($"Unknown file upload algorithm '{algorithm}'");
            }
        }
        
        var response = new BulkInitiateFileUploadResponseDto
        {
            DirectUploads = directUploadsCount > 0
                ? new BulkInitiateDirectUploadsResponseDto
                {
                    Count = directUploadsCount,
                    PreSignedMultiFileDirectUploadLink = preSignedUrlsService.GeneratePreSignedMultiFileDirectUploadUrl(
                        new PreSignedUrlsService.MultiFileDirectUploadPayload
                        {
                            WorkspaceId = workspace.Id,
                            PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                            {
                                Identity = userIdentity.Identity,
                                IdentityType = userIdentity.IdentityType
                            },
                            ExpirationDate = clock.UtcNow.AddMinutes(15),
                            BoxLinkId = boxLinkId,
                            WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption)
                        })
                }
                : null,
            MultiStepChunkUploads = multiStepChunkUploads,
            SingleChunkUploads = singleChunkUploads,

            NewWorkspaceSizeInBytes = newWorkspaceSizeInBytes ?? -1
        };

        return response;
    }

    private async ValueTask<List<UploadDetails>> HandleMultipleUploads(
        BulkInitiateFileUploadItemDto[] fileDetailsList,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken)
    {
        return workspace.Storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => HandleMultipleHardDriveUploads(
                hardDriveStorage: hardDriveStorageClient,
                fileDetailsList: fileDetailsList,
                workspace: workspace,
                workspaceEncryptionSession: workspaceEncryptionSession),

            IObjectStorageClient objectStorageClient => await HandleMultipleObjectStoreUploads(
                objectStorageClient: objectStorageClient,
                workspace: workspace,
                fileDetailsList: fileDetailsList,
                workspaceEncryptionSession: workspaceEncryptionSession,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
    }

    private List<UploadDetails> HandleMultipleHardDriveUploads(
        HardDriveStorageClient hardDriveStorage,
        BulkInitiateFileUploadItemDto[] fileDetailsList,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        return fileDetailsList
            .Select(fileDetails =>
            {
                var (name, extension) = FileNames.ToNameAndExtension(
                    fileDetails.FileNameWithExtension);

                var fileEncryptionMetadata = workspace.GenerateFileEncryptionMetadata();

                var (algorithm, filePartsCount) = hardDriveStorage.ResolveUploadAlgorithm(
                    fileSizeInBytes: fileDetails.FileSizeInBytes,
                    ikmChainStepsCount: fileEncryptionMetadata?.ChainStepSalts.Count ?? 0);

                return new UploadDetails
                {
                    FileUploadExternalId = fileDetails.FileUploadExternalId,
                    SizeInBytes = fileDetails.FileSizeInBytes,
                    FolderExternalId = fileDetails.FolderExternalId,

                    Name = workspace.EncodeMetadata(
                        name,
                        workspaceEncryptionSession),

                    Extension = workspace.EncodeMetadata(
                        extension,
                        workspaceEncryptionSession),

                    ContentType = workspace.EncodeMetadata(
                        fileDetails.FileContentType,
                        workspaceEncryptionSession),

                    StorageUploadDetails = new StorageUploadDetails
                    {
                        Algorithm = algorithm,
                        FilePartsCount = filePartsCount,
                        FileEncryptionMetadata = fileEncryptionMetadata,

                        PreSignedUploadLink = null,
                        PreSignedUploadLinkRequiredHeaders = [],
                        MultipartUploadId = string.Empty,
                        WasMultiPartUploadInitiated = false,
                    },

                    S3Key = hardDriveStorage.GenerateFileKey()
                };
            })
            .ToList();
    }

    private async ValueTask<List<UploadDetails>> HandleMultipleObjectStoreUploads(
        IObjectStorageClient objectStorageClient,
        WorkspaceContext workspace,
        BulkInitiateFileUploadItemDto[] fileDetailsList,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken)
    {
        var results = new List<UploadDetails>();
        var forAsyncProcessing = new List<BulkInitiateFileUploadItemDto>();

        for (var i = 0; i < fileDetailsList.Length; i++)
        {
            var fileDetails = fileDetailsList[i];

            var fileEncryptionMetadata = workspace.GenerateFileEncryptionMetadata();

            var (algorithm, filePartsCount) = objectStorageClient.ResolveUploadAlgorithm(
                fileSizeInBytes: fileDetails.FileSizeInBytes,
                ikmChainStepsCount: fileEncryptionMetadata?.ChainStepSalts.Count ?? 0);

            if (algorithm == UploadAlgorithm.DirectUpload)
            {
                var (name, extension) = FileNames.ToNameAndExtension(
                    fileDetails.FileNameWithExtension);

                results.Add(new UploadDetails
                {
                    FileUploadExternalId = fileDetails.FileUploadExternalId,
                    SizeInBytes = fileDetails.FileSizeInBytes,
                    FolderExternalId = fileDetails.FolderExternalId,

                    Name = workspace.EncodeMetadata(
                        name,
                        workspaceEncryptionSession),

                    Extension = workspace.EncodeMetadata(
                        extension,
                        workspaceEncryptionSession),

                    ContentType = workspace.EncodeMetadata(
                        fileDetails.FileContentType,
                        workspaceEncryptionSession),
                    
                    S3Key = objectStorageClient.GenerateFileKey(),

                    StorageUploadDetails = new StorageUploadDetails
                    {
                        Algorithm = algorithm,
                        FilePartsCount = filePartsCount,
                        FileEncryptionMetadata = fileEncryptionMetadata,

                        PreSignedUploadLink = null,
                        PreSignedUploadLinkRequiredHeaders = [],
                        MultipartUploadId = string.Empty,
                        WasMultiPartUploadInitiated = false
                    }
                });
            }
            else
            {
                forAsyncProcessing.Add(fileDetails);
            }
        }

        if (forAsyncProcessing.Any())
        {
            const int batchSize = 10;

            foreach (var batch in forAsyncProcessing.Chunk(batchSize))
            {
                var tasks = batch.Select(async fileDetails =>
                {
                    var s3Key = objectStorageClient.GenerateFileKey();
                    var fileEncryptionMetadata = workspace.GenerateFileEncryptionMetadata();

                    var (algorithm, filePartsCount) = objectStorageClient.ResolveUploadAlgorithm(
                        fileSizeInBytes: fileDetails.FileSizeInBytes,
                        ikmChainStepsCount: fileEncryptionMetadata?.ChainStepSalts.Count ?? 0);

                    string? preSignedUploadLink = null;
                    List<RequiredHeader> preSignedUploadLinkRequiredHeaders = [];
                    var multipartUploadId = string.Empty;
                    var wasMultiPartUploadInitiated = false;

                    if (algorithm == UploadAlgorithm.SingleChunkUpload)
                    {
                        var link = await objectStorageClient.GetPreSignedUploadFullFileLink(
                            bucketName: workspace.BucketName,
                            key: s3Key,
                            contentType: fileDetails.FileContentType,
                            expiresAt: clock.UtcNow.AddMinutes(15));

                        preSignedUploadLink = link.Url;
                        preSignedUploadLinkRequiredHeaders = link.RequiredHeaders;
                    }
                    else if (algorithm == UploadAlgorithm.MultiStepChunkUpload)
                    {
                        var initiatedUpload = await objectStorageClient.InitiateMultiPartUpload(
                            bucketName: workspace.BucketName,
                            key: s3Key,
                            cancellationToken: cancellationToken);

                        multipartUploadId = initiatedUpload.MultipartUploadId;
                        wasMultiPartUploadInitiated = true;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(UploadAlgorithm),
                            $"Unknown {typeof(UploadAlgorithm)} value: '{algorithm}'");
                    }

                    var (name, extension) = FileNames.ToNameAndExtension(
                        fileDetails.FileNameWithExtension);
                    
                    return new UploadDetails
                    {
                        FileUploadExternalId = fileDetails.FileUploadExternalId,
                        SizeInBytes = fileDetails.FileSizeInBytes,
                        FolderExternalId = fileDetails.FolderExternalId,
                        S3Key = s3Key,

                        Name = workspace.EncodeMetadata(
                            name,
                            workspaceEncryptionSession),

                        Extension = workspace.EncodeMetadata(
                            extension,
                            workspaceEncryptionSession),

                        ContentType = workspace.EncodeMetadata(
                            fileDetails.FileContentType,
                            workspaceEncryptionSession),

                        StorageUploadDetails = new StorageUploadDetails
                        {
                            Algorithm = algorithm,
                            FilePartsCount = filePartsCount,
                            FileEncryptionMetadata = fileEncryptionMetadata,

                            PreSignedUploadLink = preSignedUploadLink,
                            PreSignedUploadLinkRequiredHeaders = preSignedUploadLinkRequiredHeaders,
                            MultipartUploadId = multipartUploadId,
                            WasMultiPartUploadInitiated = wasMultiPartUploadInitiated
                        }
                    };
                });

                var batchResults = await Task.WhenAll(tasks);

                results.AddRange(batchResults);
            }
        }

        return results;
    }

    private async Task<BulkInsertFileUploadQuery.Result> InsertFileUploadsIntoDb(
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        List<UploadDetails> batchUploadResults,
        Dictionary<string, FolderValidationResult> folderValidationResults,
        CancellationToken cancellationToken)
    {
        var uploadsToInsert = batchUploadResults
            .Select(bu => new BulkInsertFileUploadQuery.InsertEntity
            {
                FileUploadExternalId = bu.FileUploadExternalId,
                FileExternalId = bu.S3Key.FileExternalId.Value,
                FolderId = bu.FolderExternalId is null
                    ? null
                    : folderValidationResults[bu.FolderExternalId].FolderId,
                FileName = bu.Name,
                FileContentType = bu.ContentType,
                FileExtension = bu.Extension,
                FileSizeInBytes = bu.SizeInBytes,
                KeySecretPart = bu.S3Key.KeySecretPart,

                MultipartUploadId = bu.StorageUploadDetails.MultipartUploadId,

                EncryptionKeyVersion = bu.StorageUploadDetails.FileEncryptionMetadata?.KeyVersion,
                EncryptionSalt = bu.StorageUploadDetails.FileEncryptionMetadata?.Salt,
                EncryptionNoncePrefix = bu.StorageUploadDetails.FileEncryptionMetadata?.NoncePrefix,
                EncryptionFormatVersion = bu.StorageUploadDetails.FileEncryptionMetadata?.FormatVersion,
                EncryptionChainSalts = KeyDerivationChain.Serialize(
                    bu.StorageUploadDetails.FileEncryptionMetadata?.ChainStepSalts),

                FileMetadata = null,
                ParentFileId = null
            })
            .ToArray();

        var insertFileUploadsResult = await bulkInsertFileUploadQuery.Execute(
            workspace: workspace,
            userIdentity: userIdentity,
            entities: uploadsToInsert,
            cancellationToken: cancellationToken);

        return insertFileUploadsResult;
    }

    private async Task AbortStartedMultiPartUploads(
        WorkspaceContext workspace,
        List<UploadDetails> batchUploadResults,
        CancellationToken cancellationToken)
    {
        var multiPartUploads = batchUploadResults
            .Where(bu => bu.StorageUploadDetails.WasMultiPartUploadInitiated)
            .ToList();

        if (multiPartUploads.Any())
        {
            var abortMultiPartUploadTasks = multiPartUploads.Select(mpu => workspace.Storage.AbortMultipartUpload(
                bucketName: workspace.BucketName,
                key: mpu.S3Key,
                handle: workspace.Storage.BuildAbortHandle(
                    uploadId: mpu.StorageUploadDetails.MultipartUploadId,
                    parts: []),
                cancellationToken: cancellationToken));

            await Task.WhenAll(abortMultiPartUploadTasks);
        }
    }

    private async Task PreInitializeUploadsCache(
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        List<UploadDetails> batchUploadResults,
        BulkInsertFileUploadQuery.Result insertFileUploadsResult,
        Dictionary<string, FolderValidationResult> folderValidationResults,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < batchUploadResults.Count; i++)
        {
            var upload = batchUploadResults[i];

            var insertResult = insertFileUploadsResult
                .FileUploads
                !.First(r => r.ExternalId.Value == upload.FileUploadExternalId);

            var folderAncestors = upload.FolderExternalId is not null
                && folderValidationResults.TryGetValue(upload.FolderExternalId, out var fvr)
                    ? fvr.FolderAncestors ?? []
                    : [];

            await fileUploadCache.PreInitialize(
                toCache: new FileUploadCache.FileUploadCached{
                    Id = insertResult.Id,
                    ExternalId = insertResult.ExternalId,
                    FileToUpload = new FileToUploadDetails
                    {
                        SizeInBytes = upload.SizeInBytes,
                        EncryptionMetadata = upload.StorageUploadDetails.FileEncryptionMetadata,
                        FileKey = upload.S3Key,
                        MultipartUploadId = upload.StorageUploadDetails.MultipartUploadId,
                    },
                    ContentType = upload.ContentType,
                    WorkspaceId = workspace.Id,
                    OwnerIdentity = userIdentity.Identity,
                    OwnerIdentityType = userIdentity.IdentityType,
                    FileName = upload.Name,
                    FileExtension = upload.Extension,
                    FolderAncestors = folderAncestors
                },
                cancellationToken: cancellationToken);
        }
    }

    private (FoldersValidationResultCode Code, Dictionary<string, FolderValidationResult> Results) ValidateFolders(
        WorkspaceContext workspace,
        BulkInitiateFileUploadItemDto[] fileDetailsList,
        int? boxFolderId)
    {
        if (fileDetailsList.Any(file => file.FolderExternalId is null) && boxFolderId is not null)
            return (Code: FoldersValidationResultCode.TopFolderUnauthorizedAccess, Results: []);

        if (fileDetailsList.All(file => file.FolderExternalId is null) && boxFolderId is null)
            return (Code: FoldersValidationResultCode.Ok, Results: []);

        var folderExternalIds = fileDetailsList
            .Where(file => file.FolderExternalId is not null)
            .Select(file => file.FolderExternalId!)
            .Distinct()
            .ToList();

        //situation when whole upload is pointing to single folder is so common that i decided
        //to prepare a separate path for it to avoid some unnecessary json serialization/deserialization
        //and to make the query a slightly faster
        return folderExternalIds.Count == 1
            ? ValidateSingleFolders(workspace, boxFolderId, folderExternalIds[0])
            : ValidateManyFolders(workspace, boxFolderId, folderExternalIds);
    }

    private (FoldersValidationResultCode Code, Dictionary<string, FolderValidationResult> Results) ValidateManyFolders(
        WorkspaceContext workspace,
        int? boxFolderId,
        List<string> folderExternalIds)
    {
        using var connection = plikShareDb.OpenConnection();

        var existingFolders = connection
            .Cmd(
                sql:"""
                    SELECT
                        f.fo_id,
                        f.fo_external_id,
                        (
                            SELECT json_group_array(json_object(
                                'name', sub.fo_name,
                                'externalId', sub.fo_external_id
                            ))
                            FROM (
                                SELECT af.fo_name, af.fo_external_id
                                FROM fo_folders AS af
                                WHERE af.fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                    OR af.fo_id = f.fo_id
                                ORDER BY json_array_length(af.fo_ancestor_folder_ids)
                            ) AS sub
                        )
                    FROM
                        fo_folders AS f
                    WHERE
                        f.fo_external_id IN (
                            SELECT value FROM json_each($folderExternalIds)
                        )
                        AND f.fo_workspace_id = $workspaceId
                        AND f.fo_is_being_deleted = FALSE
                        AND (
                            $boxFolderId IS NULL
                            OR f.fo_id = $boxFolderId
                            OR $boxFolderId IN (
                                SELECT value FROM json_each(f.fo_ancestor_folder_ids)
                            )
                        )
                    """,
                readRowFunc: reader => new FolderValidationResult(
                    Code: FolderValidationResultCode.Ok,
                    FolderId: reader.GetInt32(0),
                    FolderExternalId: reader.GetString(1),
                    FolderAncestors: reader.GetFromJsonOrNull<CachedFolderAncestor[]>(2) ?? []))
            .WithJsonParameter("$folderExternalIds", folderExternalIds)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();

        var missingFolders = folderExternalIds
            .Except(existingFolders.Select(ef => ef.FolderExternalId))
            .Select(id => new FolderValidationResult(
                Code: FolderValidationResultCode.NotFound,
                FolderExternalId: id,
                FolderId: -1))
            .ToArray();

        List<FolderValidationResult> results = [.. existingFolders, .. missingFolders];


        return (
            Code: missingFolders.Any()
                ? FoldersValidationResultCode.FoldersNotFound
                : FoldersValidationResultCode.Ok,

            Results: results.ToDictionary(
                keySelector: r => r.FolderExternalId,
                elementSelector: r => r)
        );
    }

    private (FoldersValidationResultCode Code, Dictionary<string, FolderValidationResult> Results) ValidateSingleFolders(
        WorkspaceContext workspace,
        int? boxFolderId,
        string folderExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var existingFolder = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         f.fo_id,
                         (
                             SELECT json_group_array(json_object(
                                 'name', sub.fo_name,
                                 'externalId', sub.fo_external_id
                             ))
                             FROM (
                                 SELECT af.fo_name, af.fo_external_id
                                 FROM fo_folders AS af
                                 WHERE af.fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                     OR af.fo_id = f.fo_id
                                 ORDER BY json_array_length(af.fo_ancestor_folder_ids)
                             ) AS sub
                         )
                     FROM
                         fo_folders AS f
                     WHERE
                         f.fo_external_id = $folderExternalId
                         AND f.fo_workspace_id = $workspaceId
                         AND f.fo_is_being_deleted = FALSE
                         AND (
                             $boxFolderId IS NULL
                             OR f.fo_id = $boxFolderId
                             OR $boxFolderId IN (
                                 SELECT value FROM json_each(f.fo_ancestor_folder_ids)
                             )
                         )
                     LIMIT 1
                     """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    Ancestors = reader.GetFromJsonOrNull<CachedFolderAncestor[]>(1) ?? []
                })
            .WithParameter("$folderExternalId", folderExternalId)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();

        if (existingFolder.IsEmpty)
        {
            return (
                Code: FoldersValidationResultCode.FoldersNotFound,

                Results: new Dictionary<string, FolderValidationResult>
                {
                    {
                        folderExternalId, new FolderValidationResult(
                            Code: FolderValidationResultCode.NotFound,
                            FolderExternalId: folderExternalId,
                            FolderId: -1)
                    }
                }
            );
        }

        return (
            Code: FoldersValidationResultCode.Ok,

            Results: new Dictionary<string, FolderValidationResult>
            {
                {
                    folderExternalId, new FolderValidationResult(
                        Code: FolderValidationResultCode.Ok,
                        FolderExternalId: folderExternalId,
                        FolderId: existingFolder.Value.Id,
                        FolderAncestors: existingFolder.Value.Ancestors)
                }
            }
        );
    }

    public record Result(
        ResultCode Code,
        BulkInitiateFileUploadResponseDto? Response = null,
        List<InitiatedFile>? InitiatedFiles = null,
        List<string>? MissingFolders = null);

    public record InitiatedFile(
        Files.Id.FileExtId FileExternalId,
        FileUploadExtId FileUploadExternalId,
        EncodedMetadataValue FileName,
        EncodedMetadataValue FileExtension,
        long SizeInBytes,
        List<EncodedMetadataValue>? FolderPath);

    public enum ResultCode
    {
        Ok = 0,
        FoldersNotFound,
        TopFolderUnauthorizedAccess,
        NotEnoughSpace
    }


    private record FolderValidationResult(
        FolderValidationResultCode Code,
        string FolderExternalId,
        int FolderId,
        CachedFolderAncestor[]? FolderAncestors = null);

    private enum FolderValidationResultCode
    {
        Ok,
        NotFound
    }

    private enum FoldersValidationResultCode
    {
        Ok,
        FoldersNotFound,
        TopFolderUnauthorizedAccess
    }

    private class UploadDetails
    {
        public required string FileUploadExternalId { get; init; }
        public required EncodedMetadataValue Name { get; init; }
        public required EncodedMetadataValue Extension { get; init; }
        public required long SizeInBytes { get; init; }
        public required EncodedMetadataValue ContentType { get; init; }
        public required string? FolderExternalId { get; init; }
        public required FileKey S3Key { get; init; }
        public required StorageUploadDetails StorageUploadDetails { get; init; }
    }

    private readonly record struct WorkspaceSpace(
        bool IsAvailable,
        long NewWorkspaceSizeInBytes);
}