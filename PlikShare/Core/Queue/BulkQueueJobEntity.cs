namespace PlikShare.Core.Queue;

public class BulkQueueJobEntity
{
    public required string JobType { get; init; }
    public required string Status { get; init; }
    public required string Definition { get; init; }
    public required int? SagaId { get; init; }
}
