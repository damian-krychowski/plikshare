namespace PlikShare.Core.Queue;

// Values are persisted in q_queue.q_job_category — keep them explicit and stable.
public enum QueueJobCategory
{
    Normal = 1,
    LongRunning = 2
}