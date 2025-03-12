namespace PlikShare.Files.BulkDelete.QueueJob;

public static class BulkDeleteS3FileQueueJobType
{
    public const int MaxChunkSize = 1000;

    public const string Value = "bulk-delete-s3-files";
}