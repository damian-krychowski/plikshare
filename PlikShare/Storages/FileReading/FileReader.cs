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
    Task WriteTo(
        PipeWriter output,
        CancellationToken cancellationToken);
}

public static class FileReader
{
    public static ValueTask<IFile> GetFile(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        WorkspaceContext workspace,
        FullEncryptionSession? fullEncryptionSession,
        CancellationToken cancellationToken)
    {
        return GetFile(
            s3FileKey,
            fileSizeInBytes,
            workspace.BucketName,
            workspace.Storage,
            fullEncryptionSession,
            cancellationToken);
    }

    public static async ValueTask<IFile> GetFile(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        string bucketName,
        IStorageClient storage,
        FullEncryptionSession? fullEncryptionSession,
        CancellationToken cancellationToken = default)
    {
        return storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => HardDriveDownloadOperation.GetFile(
                s3FileKey: s3FileKey,
                fileSizeInBytes: fileSizeInBytes,
                bucketName: bucketName,
                fullEncryptionSession: fullEncryptionSession,
                hardDriveStorageClient: hardDriveStorageClient!,
                cancellationToken: cancellationToken),

            S3StorageClient s3StorageClient => await S3DownloadOperation.GetFile(
                s3FileKey: s3FileKey,
                fileSizeInBytes: fileSizeInBytes, 
                bucketName: bucketName,
                fullEncryptionSession: fullEncryptionSession,
                s3StorageClient: s3StorageClient, 
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(storage))
        };
    }

    public static async ValueTask<IFile> GetFileRange(
        S3FileKey s3FileKey,
        FileEncryption fileEncryption,
        long fileSizeInBytes,
        BytesRange range,
        WorkspaceContext workspace,
        FullEncryptionSession? fullEncryptionSession,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        return workspace.Storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => HardDriveDownloadOperation.GetFileRange(
                s3FileKey: s3FileKey,
                fileEncryption: fileEncryption,
                fileSizeInBytes: fileSizeInBytes,
                range: range,
                bucketName: workspace.BucketName,
                fullEncryptionSession: fullEncryptionSession,
                hardDriveStorageClient: hardDriveStorageClient!,
                cancellationToken: cancellationToken),

            S3StorageClient s3StorageClient => await S3DownloadOperation.GetFileRange(
                s3FileKey: s3FileKey,
                fileEncryption: fileEncryption,
                fileSizeInBytes: fileSizeInBytes,
                range: range,
                workspace.BucketName,
                fullEncryptionSession: fullEncryptionSession,
                s3StorageClient: s3StorageClient,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
    }

    /// <exception cref="FileNotFoundInStorageException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task ReadRange(
        S3FileKey s3FileKey,
        FileEncryption fileEncryption,
        long fileSizeInBytes,
        BytesRange range,
        WorkspaceContext workspace,
        FullEncryptionSession? fullEncryptionSession,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        await using var file = await GetFileRange(
            s3FileKey,
            fileEncryption,
            fileSizeInBytes,
            range,
            workspace,
            fullEncryptionSession,
            output,
            cancellationToken);

        await file.WriteTo(
            output,
            cancellationToken);
    }
}