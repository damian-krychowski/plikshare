using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.S3;
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
        CancellationToken cancellationToken = default)
    {
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
            userIdentity,
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
            cancellationToken);
        
        var response = PrepareResponse(
            workspace: workspace, 
            userIdentity: userIdentity, 
            batchUploadResults: batchUploadResults);

        return new Result(
            Code: ResultCode.Ok,
            Response: response);
    }

    private BulkInitiateFileUploadResponseDto PrepareResponse(
        WorkspaceContext workspace, 
        IUserIdentity userIdentity,
        List<UploadDetails> batchUploadResults)
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
                    PreSignedUploadLink = upload.StorageUploadDetails.PreSignedUploadLink!
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
                            ExpirationDate = clock.UtcNow.AddMinutes(15)
                        })
                }
                : null,
            MultiStepChunkUploads = multiStepChunkUploads,
            SingleChunkUploads = singleChunkUploads
        };

        return response;
    }

    private async ValueTask<List<UploadDetails>> HandleMultipleUploads(
        BulkInitiateFileUploadItemDto[] fileDetailsList,
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        CancellationToken cancellationToken)
    {
        return workspace.Storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => HandleMultipleHardDriveUploads(
                hardDriveStorage: hardDriveStorageClient,
                fileDetailsList: fileDetailsList,
                userIdentity: userIdentity),

            S3StorageClient s3StorageClient => await HandleMultipleS3Uploads(
                s3StorageClient: s3StorageClient,
                workspace: workspace,
                fileDetailsList: fileDetailsList,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
    }

    private List<UploadDetails> HandleMultipleHardDriveUploads(
        HardDriveStorageClient hardDriveStorage,
        BulkInitiateFileUploadItemDto[] fileDetailsList,
        IUserIdentity userIdentity)
    {
        return fileDetailsList
            .Select(fileDetails =>
            {
                var (name, extension) = FileNames.ToNameAndExtension(
                    fileDetails.FileNameWithExtension);
                
                return new UploadDetails
                {
                    FileUploadExternalId = fileDetails.FileUploadExternalId,

                    Name = name,
                    Extension = extension,
                    SizeInBytes = fileDetails.FileSizeInBytes,
                    FolderExternalId = fileDetails.FolderExternalId,
                    ContentType = fileDetails.FileContentType,

                    StorageUploadDetails = hardDriveStorage.GetStorageUploadDetails(
                        fileUploadExternalId: FileUploadExtId.Parse(fileDetails.FileUploadExternalId),
                        fileSizeInBytes: fileDetails.FileSizeInBytes,
                        contentType: fileDetails.FileContentType,
                        userIdentity: userIdentity),

                    S3Key = S3FileKey.NewKey(
                        secretPart: string.Empty),
                };
            })
            .ToList();
    }

    private async ValueTask<List<UploadDetails>> HandleMultipleS3Uploads(
        S3StorageClient s3StorageClient,
        WorkspaceContext workspace,
        BulkInitiateFileUploadItemDto[] fileDetailsList,
        CancellationToken cancellationToken)
    {
        var results = new List<UploadDetails>();
        var forAsyncProcessing = new List<BulkInitiateFileUploadItemDto>();

        for (var i = 0; i < fileDetailsList.Length; i++)
        {
            var fileDetails = fileDetailsList[i];

            var (algorithm, filePartsCount) = s3StorageClient.ResolveUploadAlgorithm(
                fileSizeInBytes: fileDetails.FileSizeInBytes);

            if (algorithm == UploadAlgorithm.DirectUpload)
            {
                var (name, extension) = FileNames.ToNameAndExtension(
                    fileDetails.FileNameWithExtension);

                results.Add(new UploadDetails
                {
                    FileUploadExternalId = fileDetails.FileUploadExternalId,

                    Name = name,
                    Extension = extension,

                    ContentType = fileDetails.FileContentType,
                    SizeInBytes = fileDetails.FileSizeInBytes,
                    FolderExternalId = fileDetails.FolderExternalId,
                    S3Key = S3FileKey.NewKey(),

                    StorageUploadDetails = new StorageUploadDetails
                    {
                        Algorithm = algorithm,
                        FilePartsCount = filePartsCount,
                        FileEncryption = s3StorageClient.GenerateFileEncryptionDetails(),

                        PreSignedUploadLink = null,
                        S3UploadId = string.Empty,
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
                    var s3Key = S3FileKey.NewKey();
                    var (algorithm, filePartsCount) = s3StorageClient.ResolveUploadAlgorithm(
                        fileSizeInBytes: fileDetails.FileSizeInBytes);

                    string? preSignedUploadLink = null;
                    var s3UploadId = string.Empty;
                    var wasMultiPartUploadInitiated = false;

                    if (algorithm == UploadAlgorithm.SingleChunkUpload)
                    {
                        preSignedUploadLink = await s3StorageClient.GetPreSignedUploadFullFileLink(
                            bucketName: workspace.BucketName,
                            key: s3Key,
                            contentType: fileDetails.FileContentType,
                            cancellationToken: cancellationToken);
                    }
                    else if (algorithm == UploadAlgorithm.MultiStepChunkUpload)
                    {
                        var initiatedUpload = await s3StorageClient.InitiateMultiPartUpload(
                            bucketName: workspace.BucketName,
                            key: s3Key,
                            cancellationToken: cancellationToken);

                        s3UploadId = initiatedUpload.S3UploadId;
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

                        Name = name,
                        Extension = extension,

                        ContentType = fileDetails.FileContentType,
                        SizeInBytes = fileDetails.FileSizeInBytes,
                        FolderExternalId = fileDetails.FolderExternalId,
                        S3Key = s3Key,

                        StorageUploadDetails = new StorageUploadDetails
                        {
                            Algorithm = algorithm,
                            FilePartsCount = filePartsCount,
                            FileEncryption = s3StorageClient.GenerateFileEncryptionDetails(),

                            PreSignedUploadLink = preSignedUploadLink,
                            S3UploadId = s3UploadId,
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
                S3KeySecretPart = bu.S3Key.S3KeySecretPart,

                S3UploadId = bu.StorageUploadDetails.S3UploadId,
                EncryptionKeyVersion = bu.StorageUploadDetails.FileEncryption.Metadata?.KeyVersion,
                EncryptionSalt = bu.StorageUploadDetails.FileEncryption.Metadata?.Salt,
                EncryptionNoncePrefix = bu.StorageUploadDetails.FileEncryption.Metadata?.NoncePrefix,

                FileMetadataBlob = null,
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
            var abortMultiPartUploadTasks = multiPartUploads.Select(mpu => workspace.Storage.AbortMultiPartUpload(
                bucketName: workspace.BucketName,
                key: mpu.S3Key,
                uploadId: mpu.StorageUploadDetails.S3UploadId,
                partETags: [],
                cancellationToken: cancellationToken));

            await Task.WhenAll(abortMultiPartUploadTasks);
        }
    }

    private async Task PreInitializeUploadsCache(
        WorkspaceContext workspace,
        IUserIdentity userIdentity,
        List<UploadDetails> batchUploadResults,
        BulkInsertFileUploadQuery.Result insertFileUploadsResult,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < batchUploadResults.Count; i++)
        {
            var upload = batchUploadResults[i];

            var insertResult = insertFileUploadsResult
                .FileUploads
                !.First(r => r.ExternalId.Value == upload.FileUploadExternalId);

            await fileUploadCache.PreInitialize(
                toCache: new FileUploadCache.FileUploadCached{
                    Id = insertResult.Id,
                    ExternalId = insertResult.ExternalId,
                    FileToUpload = new FileToUploadDetails
                    {
                        SizeInBytes = upload.SizeInBytes,
                        Encryption = upload.StorageUploadDetails.FileEncryption,
                        S3FileKey = upload.S3Key,
                        S3UploadId = upload.StorageUploadDetails.S3UploadId,
                    },
                    ContentType = upload.ContentType,
                    WorkspaceId = workspace.Id,
                    OwnerIdentity = userIdentity.Identity,
                    OwnerIdentityType = userIdentity.IdentityType
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
            .Select(file => file.FolderExternalId)
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
                        fo_id,
                        fo_external_id
                    FROM 
                        fo_folders
                    WHERE 
                        fo_external_id IN (
                            SELECT value FROM json_each($folderExternalIds)
                        )
                        AND fo_workspace_id = $workspaceId
                        AND fo_is_being_deleted = FALSE
                        AND (
                            $boxFolderId IS NULL 
                            OR fo_id = $boxFolderId 
                            OR $boxFolderId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                            )
                        )
                    """,
                readRowFunc: reader => new FolderValidationResult(
                    Code: FolderValidationResultCode.Ok,
                    FolderId: reader.GetInt32(0),
                    FolderExternalId: reader.GetString(1)))
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

        var existingFolderId = connection
            .OneRowCmd(
                sql: """
                     SELECT 
                         fo_id
                     FROM 
                         fo_folders
                     WHERE 
                         fo_external_id = $folderExternalId
                         AND fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                         AND (
                             $boxFolderId IS NULL 
                             OR fo_id = $boxFolderId 
                             OR $boxFolderId IN (
                                 SELECT value FROM json_each(fo_ancestor_folder_ids)
                             )
                         )
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$folderExternalId", folderExternalId)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();
        
        if (existingFolderId.IsEmpty)
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
                        FolderId: existingFolderId.Value)
                }
            }
        );
    }

    public record Result(
        ResultCode Code,
        BulkInitiateFileUploadResponseDto? Response = default,
        List<string>? MissingFolders = default);

    public enum ResultCode
    {
        Ok = 0,
        FoldersNotFound,
        TopFolderUnauthorizedAccess
    }


    private record FolderValidationResult(
        FolderValidationResultCode Code,
        string FolderExternalId,
        int FolderId);

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
        public required string Name { get; init; }
        public required string Extension { get; init; }
        public required long SizeInBytes { get; init; }
        public required string ContentType { get; init; }
        public required string? FolderExternalId { get; init; }
        public required S3FileKey S3Key { get; init; }
        public required StorageUploadDetails StorageUploadDetails { get; init; }
    }
}