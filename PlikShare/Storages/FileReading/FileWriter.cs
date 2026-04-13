using System.Buffers;
using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.HardDrive.Upload;
using PlikShare.Storages.S3;
using PlikShare.Storages.S3.Upload;
using PlikShare.Uploads.Cache;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Storages.FileReading;

public static class FileWriter
{
    public static async Task<FilePartUploadResult> Write(
        FileToUploadDetails file,
        FilePartDetails part,
        WorkspaceContext workspace,
        FullEncryptionSession? fullEncryptionSession,
        PipeReader input,
        CancellationToken cancellationToken)
    {
        var heapBufferSize = file.Encryption.EncryptionType == StorageEncryptionType.None
            ? part.SizeInBytes
            : Aes256GcmStreaming.CalculateEncryptedPartSize(
                part.SizeInBytes, 
                part.Number);

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: heapBufferSize);

        var heapBufferMemory = heapBuffer
            .AsMemory()
            .Slice(0, heapBufferSize);

        try
        {
            if (file.Encryption.EncryptionType == StorageEncryptionType.None)
            {
                await input.CopyTo(
                    output: heapBufferMemory, 
                    sizeInBytes: part.SizeInBytes, 
                    cancellationToken: cancellationToken);
            }
            else
            {
                await input.CopyIntoBufferReadyForInPlaceEncryption(
                    output: heapBufferMemory,
                    partSizeInBytes: part.SizeInBytes,
                    partNumber: part.Number);
            }

            return await Upload(
                fileBytes: heapBufferMemory, 
                file: file, 
                part: part, 
                workspace: workspace, 
                fullEncryptionSession: fullEncryptionSession,
                cancellationToken: cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
        }
    }

    public static async Task<FilePartUploadResult> Write(
        FileToUploadDetails file,
        FilePartDetails part,
        WorkspaceContext workspace,        
        FullEncryptionSession? fullEncryptionSession,
        byte[] input,
        CancellationToken cancellationToken)
    {
        if (file.Encryption.EncryptionType == StorageEncryptionType.None)
        {
            return await Upload(
                fileBytes: input,
                file: file,
                part: part,
                workspace: workspace,
                fullEncryptionSession: fullEncryptionSession,
                cancellationToken: cancellationToken);
        }

        var heapBufferSize = Aes256GcmStreaming.CalculateEncryptedPartSize(
            part.SizeInBytes,
            part.Number);

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: heapBufferSize);

        var heapBufferMemory = heapBuffer.AsMemory().Slice(0, heapBufferSize);

        try
        {
            Aes256GcmStreaming.CopyIntoBufferReadyForInPlaceEncryption(
                input: input,
                output: heapBufferMemory,
                partSizeInBytes: part.SizeInBytes,
                partNumber: part.Number);

            return await Upload(
                fileBytes: heapBufferMemory,
                file: file,
                part: part,
                workspace: workspace,
                fullEncryptionSession: fullEncryptionSession,
                cancellationToken: cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
        }
    }

    private static async Task<FilePartUploadResult> Upload(
        Memory<byte> fileBytes,
        FileToUploadDetails file,
        FilePartDetails part,
        WorkspaceContext workspace,
        FullEncryptionSession? fullEncryptionSession,
        CancellationToken cancellationToken)
    {
        return workspace.Storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => await HardDriveUploadOperation.Execute(
                fileBytes: fileBytes,
                file: file,
                part: part,
                bucketName: workspace.BucketName,
                fullEncryptionSession: fullEncryptionSession,
                hardDriveStorage: hardDriveStorageClient!,
                cancellationToken: cancellationToken),

            S3StorageClient s3StorageClient => await S3UploadOperation.Execute(
                fileBytes: fileBytes,
                file: file,
                part: part,
                bucketName: workspace.BucketName,
                fullEncryptionSession: fullEncryptionSession,
                s3StorageClient: s3StorageClient,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
    }
}