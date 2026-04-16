using PlikShare.Uploads.Algorithm;

namespace PlikShare.Storages.FileCopying;

public class CopyFileQueueJob
{
    public required int Id { get; init; }
    public required int FileUploadId { get; init; }
    public required UploadAlgorithm UploadAlgorithm { get; init; }

    //sourceFile
    public required long FileSizeInBytes { get; init; }
    public required int SourceWorkspaceId { get; init; }
    public required S3FileKey SourceS3FileKey { get; init; }

    //new file
    public required int TargetWorkspaceId { get; init; }
    public required S3FileKey NewS3FileKey { get; init; }
    public required FileEncryption NewFileEncryption { get; init; }
    public required string S3UploadId { get; init; }
}