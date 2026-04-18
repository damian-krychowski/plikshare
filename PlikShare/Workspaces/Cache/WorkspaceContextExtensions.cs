using PlikShare.BulkDownload;
using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages;
using PlikShare.Storages.FileReading;
using System.IO.Pipelines;

namespace PlikShare.Workspaces.Cache;

public static class WorkspaceContextExtensions
{
    extension(WorkspaceContext workspace)
    {
        public ValueTask<IStorageFile> DownloadFile(
            DownloadFileDetails fileDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.DownloadFile(
                fileDetails: fileDetails,
                bucketName: workspace.BucketName,
                cancellationToken: cancellationToken);
        }

        public ValueTask<IStorageFile> DownloadFileRange(
            DownloadFileRangeDetails fileDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.DownloadFileRange(
                fileDetails: fileDetails,
                bucketName: workspace.BucketName,
                cancellationToken: cancellationToken);
        }

        public Task DownloadFilesInBulk(
            BulkDownloadDetails bulkDownloadDetails,
            PipeWriter responsePipeWriter,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.DownloadFilesInBulk(
                bulkDownloadDetails, 
                workspace.BucketName, 
                responsePipeWriter, 
                cancellationToken);
        }

        public ValueTask<FilePartUploadResult> UploadFilePart(
            PipeReader input,
            UploadFilePartDetails uploadDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.UploadFilePart(
                input: input, 
                uploadDetails: uploadDetails, 
                bucketName: workspace.BucketName, 
                cancellationToken: cancellationToken);
        }

        public ValueTask<FilePartUploadResult> UploadFilePart(
            byte[] input,
            UploadFilePartDetails uploadDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.UploadFilePart(
                input: input,
                uploadDetails: uploadDetails, 
                bucketName: workspace.BucketName, 
                cancellationToken: cancellationToken);
        }

        public async ValueTask ReadRange(
            DownloadFileRangeDetails details,
            PipeWriter output,
            CancellationToken cancellationToken)
        {
            await using var storageFile = await workspace.Storage.DownloadFileRange(
                fileDetails: details,
                bucketName: workspace.BucketName,
                cancellationToken: cancellationToken);

            await storageFile.ReadTo(
                output,
                cancellationToken);
        }
    }
}