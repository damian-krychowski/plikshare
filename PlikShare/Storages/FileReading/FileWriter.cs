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
        FilePartUpload part,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        PipeReader input,
        CancellationToken cancellationToken)
    {
        var heapBufferSize = file
            .EncryptionMetadata
            .CalculateBufferSize(part.Part);

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: heapBufferSize);

        var heapBufferMemory = heapBuffer
            .AsMemory()
            .Slice(0, heapBufferSize);

        try
        {
            if (file.EncryptionMetadata is null)
            {
                await input.CopyTo(
                    output: heapBufferMemory, 
                    sizeInBytes: part.Part.SizeInBytes, 
                    cancellationToken: cancellationToken);
            }
            else if (file.EncryptionMetadata.FormatVersion == 1)
            {
                await input.CopyIntoBufferReadyForInPlaceEncryption(
                    output: heapBufferMemory,
                    filePart: part.Part);
            }
            else if (file.EncryptionMetadata.FormatVersion == 2)
            {
                await input.CopyIntoBufferReadyForInPlaceEncryption(
                    output: heapBufferMemory,
                    filePart: part.Part,
                    chainStepsCount: file.EncryptionMetadata.ChainStepSalts.Count);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported file encryption format version '{file.EncryptionMetadata.FormatVersion}' " +
                    $"for file '{file.S3FileKey.FileExternalId}'.");
            }

            return await Upload(
                fileBytes: heapBufferMemory, 
                file: file, 
                part: part, 
                workspace: workspace, 
                workspaceEncryptionSession: workspaceEncryptionSession,
                cancellationToken: cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
        }
    }
    
    public static async Task<FilePartUploadResult> Write(
        FileToUploadDetails file,
        FilePartUpload part,
        WorkspaceContext workspace,        
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        byte[] input,
        CancellationToken cancellationToken)
    {
        if (file.EncryptionMetadata is null)
        {
            return await Upload(
                fileBytes: input,
                file: file,
                part: part,
                workspace: workspace,
                workspaceEncryptionSession: workspaceEncryptionSession,
                cancellationToken: cancellationToken);
        }

        var heapBufferSize = file
            .EncryptionMetadata
            .CalculateBufferSize(part.Part);

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: heapBufferSize);

        var heapBufferMemory = heapBuffer
            .AsMemory()
            .Slice(0, heapBufferSize);

        try
        {
            if (file.EncryptionMetadata.FormatVersion == 1)
            {
                Aes256GcmStreamingV1.CopyIntoBufferReadyForInPlaceEncryption(
                    input: input,
                    output: heapBufferMemory,
                    filePart: part.Part);
            }
            else if (file.EncryptionMetadata.FormatVersion == 2)
            {
                Aes256GcmStreamingV2.CopyIntoBufferReadyForInPlaceEncryption(
                    input: input,
                    output: heapBufferMemory,
                    filePart: part.Part,
                    chainStepsCount: file.EncryptionMetadata.ChainStepSalts.Count);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported file encryption format version '{file.EncryptionMetadata.FormatVersion}' " +
                    $"for file '{file.S3FileKey.FileExternalId}'.");
            }

            return await Upload(
                fileBytes: heapBufferMemory,
                file: file,
                part: part,
                workspace: workspace,
                workspaceEncryptionSession: workspaceEncryptionSession,
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
        FilePartUpload part,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken)
    {
        return workspace.Storage switch
        {
            HardDriveStorageClient hardDriveStorageClient => await HardDriveUploadOperation.Execute(
                fileBytes: fileBytes,
                file: file,
                part: part,
                bucketName: workspace.BucketName,
                workspaceEncryptionSession: workspaceEncryptionSession,
                hardDriveStorage: hardDriveStorageClient!,
                cancellationToken: cancellationToken),

            S3StorageClient s3StorageClient => await S3UploadOperation.Execute(
                fileBytes: fileBytes,
                file: file,
                part: part,
                bucketName: workspace.BucketName,
                workspaceEncryptionSession: workspaceEncryptionSession,
                s3StorageClient: s3StorageClient,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(workspace.Storage))
        };
    }
}