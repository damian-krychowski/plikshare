namespace PlikShare.Core.Queue;

public class QueueJobInfoProvider
{
    private readonly IReadOnlyDictionary<string, QueueJobCategory> _jobTypesMap;
    private readonly IReadOnlyDictionary<string, int> _jobPriorityMap;

    // Maps are built once at registration time from the executor TYPES (see QueueJobInfoBuilder) and
    // injected ready-made — the provider has no dependency on the executors, so it can't take part in
    // the IQueue -> Queue -> provider dependency cycle.
    public QueueJobInfoProvider(
        IReadOnlyDictionary<string, QueueJobCategory> jobTypesMap,
        IReadOnlyDictionary<string, int> jobPriorityMap)
    {
        _jobTypesMap = jobTypesMap;
        _jobPriorityMap = jobPriorityMap;
    }

    public QueueJobCategory GetJobCategory(string jobType)
    {
        if (!_jobTypesMap.TryGetValue(jobType, out var category))
        {
            throw new ArgumentOutOfRangeException(
                $"QueueJobType '{jobType}' was not found in QueueJobInfoProvider directory");
        }

        return category;
    }

    public int GetJobPriority(string jobType)
    {
        if (!_jobPriorityMap.TryGetValue(jobType, out var priority))
        {
            throw new ArgumentOutOfRangeException(
                $"QueueJobType '{jobType}' was not found in QueueJobInfoProvider directory");
        }

        return priority;
    }
}
