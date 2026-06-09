namespace PlikShare.Core.Queue;

public class BulkQueueJobEntity
{
    public required string JobType { get; init; }
    public required string Status { get; init; }
    public required string Definition { get; init; }
    public required int? SagaId { get; init; }
    
    public required int JobCategory { get; init; }
    public required int JobPriority { get; init; }

    /// <summary>Generic batch grouping key — written to <c>q_batch_id</c>. Null for jobs not part of a batch.</summary>
    public Guid? BatchId { get; init; }

    /// <summary>This job's contribution to its batch's item total — written to <c>q_batch_items_count</c>. Null for jobs not part of a batch.</summary>
    public int? BatchItemsCount { get; init; }
}
