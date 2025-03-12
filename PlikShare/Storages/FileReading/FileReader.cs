using System.IO.Pipelines;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.HardDrive.Download;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.S3;
using PlikShare.Storages.S3.Download;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Storages.FileReading;

public static class FileReader
{
    /// <exception cref="FileNotFoundInStorageException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static Task ReadFull(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        WorkspaceContext workspace,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        return ReadFull(
            s3FileKey,
            fileSizeInBytes,
            workspace.BucketName,
            workspace.Storage,
            output,
            cancellationToken);
    }

    public static Task ReadFull(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        string bucketName,
        IStorageClient storage,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        return storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => HardDriveDownloadOperation.ExecuteForFullFile(
                s3FileKey: s3FileKey,
                fileSizeInBytes: fileSizeInBytes,
                bucketName: bucketName,
                hardDriveStorageClient: hardDriveStorageClient!,
                output: output,
                cancellationToken: cancellationToken),

            S3StorageClient s3StorageClient => S3DownloadOperation.ExecuteForFullFile(
                s3FileKey: s3FileKey,
                fileSizeInBytes: fileSizeInBytes,
                bucketName: bucketName,
                s3StorageClient: s3StorageClient,
                output: output,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(storage))
        };
    }

    /// <exception cref="FileNotFoundInStorageException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static Task ReadRange(
        S3FileKey s3FileKey,
        FileEncryption fileEncryption,
        long fileSizeInBytes,
        BytesRange range,
        WorkspaceContext workspace,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        return workspace.Storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => HardDriveDownloadOperation.ExecuteForRange(
                s3FileKey: s3FileKey,
                fileEncryption: fileEncryption,
                fileSizeInBytes: fileSizeInBytes,
                range: range,
                bucketName: workspace.BucketName,
                hardDriveStorageClient: hardDriveStorageClient!,
                output: output,
                cancellationToken: cancellationToken),

            S3StorageClient s3StorageClient => S3DownloadOperation.ExecuteForRange(
                s3FileKey: s3FileKey,
                fileEncryption: fileEncryption,
                fileSizeInBytes: fileSizeInBytes,
                range: range,
                workspace.BucketName,
                s3StorageClient: s3StorageClient,
                output: output,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
    }
}