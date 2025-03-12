namespace PlikShare.Core.Queue;

public class QueueJobInfoProvider
{
    private readonly Dictionary<string, QueueJobCategory> _jobTypesMap =
        new(comparer: StringComparer.InvariantCultureIgnoreCase);


    private readonly Dictionary<string, int> _jobPriorityMap =
        new(comparer: StringComparer.InvariantCultureIgnoreCase);

    public QueueJobInfoProvider(
        IEnumerable<IQueueNormalJobExecutor> normalJobExecutors,
        IEnumerable<IQueueDbOnlyJobExecutor> dbOnlyJobExecutors,
        IEnumerable<IQueueLongRunningJobExecutor> longRunningJobExecutors)
    {
        foreach (var queueJobExecutor in normalJobExecutors)
        {
            _jobTypesMap.Add(
                key: queueJobExecutor.JobType,
                value: QueueJobCategory.Normal);

            _jobPriorityMap.Add(
                key: queueJobExecutor.JobType,
                value: queueJobExecutor.Priority);
        }

        foreach (var queueDbOnlyJobExecutor in dbOnlyJobExecutors)
        {
            _jobTypesMap.Add(
                key: queueDbOnlyJobExecutor.JobType,
                value: QueueJobCategory.DbOnly);

            _jobPriorityMap.Add(
                key: queueDbOnlyJobExecutor.JobType,
                value: queueDbOnlyJobExecutor.Priority);
        }

        foreach (var queueLongRunningJobExecutor in longRunningJobExecutors)
        {
            _jobTypesMap.Add(
                key: queueLongRunningJobExecutor.JobType,
                value: QueueJobCategory.LongRunning);

            _jobPriorityMap.Add(
                key: queueLongRunningJobExecutor.JobType,
                value: queueLongRunningJobExecutor.Priority);
        }
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