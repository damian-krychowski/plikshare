namespace PlikShare.Core.Queue;

public sealed record BulkQueueJobWithTrackedFiles(
    BulkQueueJobEntity Job,
    IReadOnlyList<int> TrackedFileIds);
