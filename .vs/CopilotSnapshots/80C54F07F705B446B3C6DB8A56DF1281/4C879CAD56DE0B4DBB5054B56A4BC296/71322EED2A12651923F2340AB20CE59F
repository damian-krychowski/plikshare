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

        var heapBufferMemory = heapBuffer.AsMemory().Slice(0, heapBufferSize);

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

            return workspace.Storage switch
            {
                HardDriveStorageClient hardDriveStorageClient => await HardDriveUploadOperation.Execute(
                    fileBytes: heapBufferMemory,
                    file: file,
                    part: part,
                    bucketName: workspace.BucketName,
                    hardDriveStorage: hardDriveStorageClient!,
                    cancellationToken: cancellationToken),

                S3StorageClient s3StorageClient => await S3UploadOperation.Execute(
                    fileBytes: heapBufferMemory,
                    file: file,
                    part: part,
                    bucketName: workspace.BucketName,
                    s3StorageClient: s3StorageClient,
                    cancellationToken: cancellationToken),

                _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
        }
    }
}