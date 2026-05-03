using PlikShare.Storages;

namespace PlikShare.Files.BulkDelete.QueueJob;

public class BulkDeleteFilesQueueJobDefinition
{
    public required int StorageId { get; init; }
    public required string BucketName { get; init; }
    public required FileKey[] FileKeys { get; init; }
}
