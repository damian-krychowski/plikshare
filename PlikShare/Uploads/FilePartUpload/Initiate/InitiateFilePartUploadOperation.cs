using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.Chunking;
using PlikShare.Uploads.FilePartUpload.Initiate.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Uploads.FilePartUpload.Initiate;

public class InitiateFilePartUploadOperation(
    FileUploadCache fileUploadCache,
    PreSignedUrlsService preSignedUrlsService,
    IMasterDataEncryption masterDataEncryption,
    IClock clock)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileUploadExtId fileUploadExternalId,
        int partNumber,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken)
    {
        var fileUpload = await GetFileUploadFromCache(
            fileUploadExternalId: fileUploadExternalId, 
            workspace: workspace,
            owner: userIdentity,
            cancellationToken: cancellationToken);

        if (fileUpload is null)
        {
            Log.Warning("Could not initiate FileUpload '{FileUploadExternalId}' part '{FileUploadPartNumber}' because FileUpload was not found",
                fileUploadExternalId,
                partNumber);
            
            return new Result(Code: ResultCode.FileUploadNotFound);
        }

        var ikmChainStepsCount = fileUpload.FileToUpload.EncryptionMetadata?.ChainStepSalts.Count ?? 0;

        var isPartNumberAllowed = FileParts.IsPartNumberAllowed(
            fileSizeInBytes: fileUpload.FileToUpload.SizeInBytes,
            partNumber: partNumber,
            storageEncryptionType: workspace.Storage.EncryptionType,
            ikmChainStepsCount: ikmChainStepsCount);

        if (!isPartNumberAllowed)
        {
            Log.Warning("Could not initiate FileUpload '{FileUploadExternalId}' part '{FileUploadPartNumber}' because FileUploadPart was not allowed",
                fileUploadExternalId,
                partNumber);
            
            return new Result(Code: ResultCode.FileUploadPartNumberNotAllowed);
        }

        var preSignedUrlResult = await GetPreSignedUploadLink(
            workspace,
            fileUploadExternalId,
            partNumber,
            boxLinkId,
            userIdentity,
            enforceInternalPassThrough,
            workspaceEncryptionSession,
            fileUpload);

        var (startsAtByte, endsAtByte) = FileParts.GetPartByteRange(
            fileSizeInBytes: fileUpload.FileToUpload.SizeInBytes,
            partNumber: partNumber,
            storageEncryptionType: workspace.Storage.EncryptionType,
            ikmChainStepsCount: ikmChainStepsCount);

        Log.Debug("FileUpload '{FileUploadExternalId}' part '{FileUploadPartNumber}' " +
                  "(Bytes: {StartsAtByte}-{EndsAtByte}) was initiated.",
            fileUploadExternalId,
            startsAtByte,
            endsAtByte,
            partNumber);

        return new Result(
            Code: ResultCode.FilePartUploadInitiated,
            Details: new FilePartUploadDetails(
                UploadPreSignedUrl: preSignedUrlResult.Url,
                StartsAtByte: startsAtByte,
                EndsAtByte: endsAtByte,
                CompleteCallback: preSignedUrlResult.CompleteCallback));
    }

    private async ValueTask<PreSignedUploadLinkResult> GetPreSignedUploadLink(
        WorkspaceContext workspace, 
        FileUploadExtId fileUploadExternalId,
        int partNumber, 
        int? boxLinkId, 
        IUserIdentity userIdentity, 
        bool enforceInternalPassThrough,
        WorkspaceEncryptionSession? workspaceEncryptionSession, 
        FileUploadContext fileUpload)
    {
        return workspace.Storage switch
        {
            HardDriveStorageClient => HandleHardDriveGetPreSignedUploadFilePartLink(
                fileUploadExternalId: fileUploadExternalId,
                partNumber: partNumber,
                contentType: workspaceEncryptionSession.DecodeEncryptableMetadata(fileUpload.ContentType),
                boxLinkId: boxLinkId, 
                userIdentity: userIdentity, 
                workspaceEncryptionSession: workspaceEncryptionSession),

            IObjectStorageClient objectStorageClient => await HandleStorageObjectPreSignedUploadFilePartLink(
                objectStorageClient: objectStorageClient,
                bucketName: workspace.BucketName,
                fileUpload: fileUpload, 
                fileUploadExternalId: fileUploadExternalId, 
                partNumber: partNumber, 
                boxLinkId: boxLinkId, 
                userIdentity: userIdentity, 
                enforceInternalPassThrough: enforceInternalPassThrough, 
                workspaceEncryptionSession: workspaceEncryptionSession),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
    }

    private PreSignedUploadLinkResult HandleHardDriveGetPreSignedUploadFilePartLink(
        FileUploadExtId fileUploadExternalId,
        int partNumber,
        string contentType,
        int? boxLinkId,
        IUserIdentity userIdentity,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var url = preSignedUrlsService.GeneratePreSignedUploadUrl(
            payload: new PreSignedUrlsService.UploadPayload
            {
                FileUploadExternalId = fileUploadExternalId,
                PartNumber = partNumber,
                ContentType = contentType,
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ExpirationDate = clock.UtcNow.Add(TimeSpan.FromMinutes(1)),
                BoxLinkId = boxLinkId,
                WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption),
            });

        // HardDrive routes the upload through PlikShare's own pre-signed endpoint,
        // which records the part directly — no follow-up complete-part call needed.
        var result = new PreSignedUploadLinkResult
        {
            Url = url,
            CompleteCallback = null
        };

        return result;
    }

    private async ValueTask<PreSignedUploadLinkResult> HandleStorageObjectPreSignedUploadFilePartLink(
        IObjectStorageClient objectStorageClient,
        string bucketName,
        FileUploadContext fileUpload,
        FileUploadExtId fileUploadExternalId,
        int partNumber,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var contentType = workspaceEncryptionSession.DecodeEncryptableMetadata(
            fileUpload.ContentType);

        if (objectStorageClient.Encryption is ManagedStorageEncryption or FullStorageEncryption || enforceInternalPassThrough)
        {
            var url = preSignedUrlsService.GeneratePreSignedUploadUrl(
                new PreSignedUrlsService.UploadPayload
                {
                    FileUploadExternalId = fileUploadExternalId,
                    PartNumber = partNumber,
                    ContentType = contentType,
                    PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                    {
                        Identity = userIdentity.Identity,
                        IdentityType = userIdentity.IdentityType
                    },
                    ExpirationDate = clock.UtcNow.Add(TimeSpan.FromMinutes(1)),
                    BoxLinkId = boxLinkId,
                    WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption)
                });

            // Encryption proxy: upload routes through PlikShare's own pre-signed
            // endpoint, which records the part directly.
            return new PreSignedUploadLinkResult
            {
                Url = url,
                CompleteCallback = null
            };
        }

        if (objectStorageClient.Encryption is NoStorageEncryption)
        {
            var link = await objectStorageClient.GetPreSignedUploadFilePartLink(
                bucketName,
                fileUpload.FileToUpload.FileKey,
                fileUpload.FileToUpload.MultipartUploadId,
                partNumber,
                contentType,
                clock.UtcNow.AddMinutes(15));

            // Direct-to-storage upload: client must call complete-part to record
            // that the part was uploaded. The backend-specific verification token
            // (or absence thereof) is captured in ETagSourceHeader.
            return new PreSignedUploadLinkResult
            {
                Url = link.Url,
                CompleteCallback = new CompleteFilePartUploadCallbackDto(
                    ETagSourceHeader: link.ETagSourceHeader)
            };
        }

        throw new NotImplementedException($"Unknown encryption type: '{objectStorageClient.Encryption.GetType()}'");
    }

    private async ValueTask<FileUploadContext?> GetFileUploadFromCache(
        FileUploadExtId fileUploadExternalId, 
        WorkspaceContext workspace,
        IUserIdentity owner,
        CancellationToken cancellationToken)
    {
        //file upload cache is being initialized during InitiateFileUpload phase
        //thanks to the cache we save a lot of unnecessary DB calls
        //especially for large files, which can have hundreds of parts
        //because cache o only stores info about FileUploadExternalId
        //we additionally manually check if upload belongs to given user
        //to avoid any potential leaks

        var upload = await fileUploadCache.GetFileUpload(
            uploadExternalId: fileUploadExternalId,
            cancellationToken: cancellationToken);

        if (upload is null)
            return null;

        if (upload.Workspace.Id != workspace.Id)
            return null;

        if (!(upload.OwnerIdentity == owner.Identity && upload.OwnerIdentityType == owner.IdentityType))
            return null;

        return upload;
    }
    public record Result(
        ResultCode Code,
        FilePartUploadDetails? Details = default);

    public record FilePartUploadDetails(
        string UploadPreSignedUrl,
        long StartsAtByte,
        long EndsAtByte,
        CompleteFilePartUploadCallbackDto? CompleteCallback);

    private sealed class PreSignedUploadLinkResult
    {
        public required string Url { get; init; }
        public required CompleteFilePartUploadCallbackDto? CompleteCallback { get; init; }
    }

    public enum ResultCode
    {
        FilePartUploadInitiated = 0,
        FileUploadNotFound,
        FileUploadPartNumberNotAllowed
    }
}