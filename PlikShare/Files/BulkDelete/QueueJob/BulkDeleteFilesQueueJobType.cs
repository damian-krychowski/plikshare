namespace PlikShare.Files.BulkDelete.QueueJob;

public static class BulkDeleteFilesQueueJobType
{
    public const int MaxChunkSize = 1000;

    public const string Value = "bulk-delete-files";
}
