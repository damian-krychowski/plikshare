namespace PlikShare.Core.Queue;

public sealed record QueueJobBatch(
    Guid Id,
    int ItemsCount);
