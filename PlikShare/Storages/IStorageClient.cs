using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using PlikShare.Storages.Id;
using PlikShare.Trash;
using PlikShare.Uploads.Algorithm;

namespace PlikShare.Storages;

public interface IStorageClient
{
    int StorageId { get; }
    StorageExtId ExternalId { get; }
    string Name { get; }
    public StorageEncryption Encryption { get; }

    // Settable: an admin can change the storage's default trash policy at runtime via the
    // UpdateDefaultTrashPolicy endpoint, which refreshes this on the cached client so newly
    // created workspaces snapshot the up-to-date value.
    public TrashPolicy DefaultTrashPolicy { get; set; }

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
        FileKey key,
        CancellationToken cancellationToken);

    ValueTask DeleteFiles(
        string bucketName,
        FileKey[] keys,
        CancellationToken cancellationToken);

    Task CompleteMultiPartUpload(
        string bucketName,
        FileKey key,
        string uploadId,
        List<UploadedFilePart> partETags,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds the backend-specific abort payload from the primitive state PlikShare
    /// keeps in DB (multipart upload id from <c>fu_file_uploads</c>, parts from
    /// <c>fup_file_upload_parts</c>). Producers call this at the point of
    /// enqueueing an abort job; backends ignore whichever inputs they don't need.
    /// </summary>
    MultipartUploadAbortHandle BuildAbortHandle(
        string uploadId,
        IReadOnlyList<UploadedFilePart> parts);

    Task AbortMultipartUpload(
        string bucketName,
        FileKey key,
        MultipartUploadAbortHandle handle,
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

    FileKey GenerateFileKey();
    string GenerateFileKeySecretPart();
}

public record UploadFilePartDetails(
    FileKey FileKey,
    string? MultipartUploadId,
    long FileSizeInBytes,
    FilePart Part,
    FileEncryptionMode EncryptionMode,
    UploadAlgorithm UploadAlgorithm);

public record DownloadFileDetails(
    FileKey FileKey,
    long FileSizeInBytes,
    FileEncryptionMode EncryptionMode);

public record DownloadFileRangeDetails(
    BytesRange Range,
    FileKey FileKey,
    long FileSizeInBytes,
    FileEncryptionMode EncryptionMode);

/// <summary>
/// A part that finished uploading. <see cref="ETag"/> is non-null for backends that
/// return an ETag on the part-upload response (S3-compatible APIs, HardDrive); it is
/// <c>null</c> for backends where the join key is reconstructed server-side from the
/// part number — Azure Block Blob, where deterministic block IDs are committed via
/// PutBlockList without needing any client-supplied token.
/// </summary>
public record UploadedFilePart(int PartNumber, string? ETag);