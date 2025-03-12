using PlikShare.Core.UserIdentity;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.Chunking;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Uploads.FilePartUpload.Initiate;

public class InitiateFilePartUploadOperation(
    FileUploadCache fileUploadCache)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileUploadExtId fileUploadExternalId,
        int partNumber,
        IUserIdentity userIdentity,
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

        var isPartNumberAllowed = FileParts.IsPartNumberAllowed(
            fileSizeInBytes: fileUpload.FileToUpload.SizeInBytes,
            partNumber: partNumber,
            storageEncryptionType: workspace.Storage.EncryptionType);

        if (!isPartNumberAllowed)
        {
            Log.Warning("Could not initiate FileUpload '{FileUploadExternalId}' part '{FileUploadPartNumber}' because FileUploadPart was not allowed",
                fileUploadExternalId,
                partNumber);
            
            return new Result(Code: ResultCode.FileUploadPartNumberNotAllowed);
        }

        var preSignedUrl = await workspace
            .Storage
            .GetPreSignedUploadFilePartLink(
                bucketName: workspace.BucketName,
                fileUploadExternalId: fileUploadExternalId,
                key: fileUpload.FileToUpload.S3FileKey,
                uploadId: fileUpload.FileToUpload.S3UploadId,
                partNumber: partNumber,
                contentType: fileUpload.ContentType,
                userIdentity: userIdentity,
                cancellationToken: cancellationToken);

        var (startsAtByte, endsAtByte) = FileParts.GetPartByteRange(
            fileSizeInBytes: fileUpload.FileToUpload.SizeInBytes,
            partNumber: partNumber,
            storageEncryptionType: workspace.Storage.EncryptionType);

        Log.Debug("FileUpload '{FileUploadExternalId}' part '{FileUploadPartNumber}' " +
                  "(Bytes: {StartsAtByte}-{EndsAtByte}) was initiated.",
            fileUploadExternalId,
            startsAtByte,
            endsAtByte,
            partNumber);

        return new Result(
            Code: ResultCode.FilePartUploadInitiated,
            Details: new FilePartUploadDetails(
                UploadPreSignedUrl: preSignedUrl,
                StartsAtByte: startsAtByte,
                EndsAtByte: endsAtByte,
                IsCompleteFilePartUploadCallbackRequired: workspace
                    .Storage
                    .IsCompleteFilePartUploadCallbackRequired()));
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
        bool IsCompleteFilePartUploadCallbackRequired);
    
    public enum ResultCode
    {
        FilePartUploadInitiated = 0,
        FileUploadNotFound,
        FileUploadPartNumberNotAllowed
    }
}