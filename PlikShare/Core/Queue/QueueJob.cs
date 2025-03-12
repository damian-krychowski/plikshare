namespace PlikShare.Core.Queue;

public readonly record struct QueueJob(
    int Id,
    Guid CorrelationId,
    string JobType,
    string DefinitionJson,
    DateTimeOffset EnqueuedAt,
    DateTimeOffset ExecuteAfterDate,
    int FailedRetriesCount)
{
    public string Identity => $"{JobType}#{Id}";
}

public readonly record struct QueueSagaId(
    int Value);

public readonly record struct QueueJobId(
    int Value);

public class QueueSagaJob
{
    public required int Id { get; init; }
    public required string JobType { get; init; }
    public required string Definition { get; init; }
    public required Guid CorrelationId { get; init; }
    public required string Status { get; init; }
}