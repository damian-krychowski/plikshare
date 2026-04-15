using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.HardDrive.Download;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.S3;
using PlikShare.Storages.S3.Download;
using PlikShare.Workspaces.Cache;
using System.IO.Pipelines;

namespace PlikShare.Storages.FileReading;

public interface IFile: IDisposable, IAsyncDisposable
{
    ValueTask WriteTo(
        PipeWriter output,
        CancellationToken cancellationToken);
}

public static class FileReader
{
    public static ValueTask<IFile> GetFile(
        S3FileKey s3FileKey,
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        WorkspaceContext workspace,
        CancellationToken cancellationToken)
    {
        return GetFile(
            s3FileKey,
            fileEncryptionMetadata,
            fileSizeInBytes,
            workspaceEncryptionSession,
            workspace.BucketName,
            workspace.Storage,
            cancellationToken);
    }

    public static async ValueTask<IFile> GetFile(
        S3FileKey s3FileKey,
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        string bucketName,
        IStorageClient storage,
        CancellationToken cancellationToken = default)
    {
        return storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => HardDriveDownloadOperation.GetFile(
                s3FileKey: s3FileKey,
                fileEncryptionMetadata: fileEncryptionMetadata,
                fileSizeInBytes: fileSizeInBytes,
                workspaceEncryptionSession: workspaceEncryptionSession,
                bucketName: bucketName,
                hardDriveStorageClient: hardDriveStorageClient),

            S3StorageClient s3StorageClient => await S3DownloadOperation.GetFile(
                s3FileKey: s3FileKey,
                fileEncryptionMetadata: fileEncryptionMetadata,
                fileSizeInBytes: fileSizeInBytes, 
                workspaceEncryptionSession: workspaceEncryptionSession,
                bucketName: bucketName,
                s3StorageClient: s3StorageClient, 
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(storage))
        };
    }

    public static async ValueTask<IFile> GetFileRange(
        S3FileKey s3FileKey,
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        BytesRange range,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        return workspace.Storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => HardDriveDownloadOperation.GetFileRange(
                s3FileKey: s3FileKey,
                fileEncryptionMetadata: fileEncryptionMetadata,
                fileSizeInBytes: fileSizeInBytes,
                range: range,
                workspaceEncryptionSession: workspaceEncryptionSession,
                bucketName: workspace.BucketName,
                hardDriveStorageClient: hardDriveStorageClient),

            S3StorageClient s3StorageClient => await S3DownloadOperation.GetFileRange(
                s3FileKey: s3FileKey,
                fileEncryptionMetadata: fileEncryptionMetadata,
                fileSizeInBytes: fileSizeInBytes,
                range: range,
                workspaceEncryptionSession: workspaceEncryptionSession,
                bucketName: workspace.BucketName,
                s3StorageClient: s3StorageClient,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
    }

    /// <exception cref="FileNotFoundInStorageException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task ReadRange(
        S3FileKey s3FileKey,
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        BytesRange range,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        await using var file = await GetFileRange(
            s3FileKey,
            fileEncryptionMetadata,
            fileSizeInBytes,
            range,
            workspace,
            workspaceEncryptionSession,
            output,
            cancellationToken);

        await file.WriteTo(
            output,
            cancellationToken);
    }
}