using System.IO.Pipelines;
using Amazon.S3.Model;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Id;

namespace PlikShare.Storages;

public interface IStorageClient
{
    int StorageId { get; }
    StorageExtId ExternalId { get; }
    string Name { get; }
    public StorageEncryption Encryption { get; }

    ValueTask<IStorageFile> DownloadFile(
        DownloadFileDetails fileDetails,
        string bucketName,
        CancellationToken cancellationToken);

    ValueTask<IStorageFile> DownloadFileRange(
        DownloadFileRangeDetails fileDetails,
        string bucketName,
        CancellationToken cancellationToken);

    ValueTask<FilePartUploadResult> UploadFilePart(
        PipeReader input,
        UploadFilePartDetails uploadDetails,
        string bucketName,
        CancellationToken cancellationToken);

    ValueTask<FilePartUploadResult> UploadFilePart(
        Memory<byte> input,
        UploadFilePartDetails uploadDetails,
        string bucketName,
        CancellationToken cancellationToken);

    ValueTask DeleteFile(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken);

    ValueTask DeleteFiles(
        string bucketName,
        S3FileKey[] keys,
        CancellationToken cancellationToken);

    Task CompleteMultiPartUpload(
        string bucketName,
        S3FileKey key,
        string uploadId,
        List<PartETag> partETags,
        CancellationToken cancellationToken);
    
    ValueTask<PreSignedUploadLinkResult> GetPreSignedUploadFilePartLink(
        string bucketName,
        FileUploadExtId fileUploadExternalId,
        S3FileKey key,
        string uploadId,
        int partNumber,
        string contentType,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken);

    ValueTask<string> GetPreSignedDownloadFileLink(
        string bucketName,
        S3FileKey key,
        string contentType,
        string fileName,
        ContentDispositionType contentDisposition,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken);

    Task AbortMultiPartUpload(
        string bucketName,
        S3FileKey key,
        string uploadId,
        List<string> partETags,
        CancellationToken cancellationToken);
    
    Task CreateBucketIfDoesntExist(
        string bucketName,
        CancellationToken cancellationToken);

    Task DeleteBucket(
        string bucketName,
        CancellationToken cancellationToken);
    
    (UploadAlgorithm Algorithm, int FilePartsCount) ResolveUploadAlgorithm(
        long fileSizeInBytes,
        int ikmChainStepsCount);

    (UploadAlgorithm Algorithm, int FilePartsCount) ResolveCopyUploadAlgorithm(
        long fileSizeInBytes,
        int ikmChainStepsCount);

    string GenerateFileS3KeySecretPart();
}

public class PreSignedUploadLinkResult
{
    public required string Url { get; init; }
    public required bool IsCompleteFilePartUploadCallbackRequired { get; init; }
}

public record UploadFilePartDetails(
    S3FileKey S3FileKey,
    string? S3UploadId,
    long FileSizeInBytes,
    FilePart Part,
    FileEncryptionMode EncryptionMode,
    UploadAlgorithm UploadAlgorithm);

public record DownloadFileDetails(
    S3FileKey S3FileKey,
    long FileSizeInBytes,
    FileEncryptionMode EncryptionMode);

public record DownloadFileRangeDetails(
    BytesRange Range,
    S3FileKey S3FileKey,
    long FileSizeInBytes,
    FileEncryptionMode EncryptionMode);