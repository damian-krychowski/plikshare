using PlikShare.Core.Emails;

namespace PlikShare.Core.Queue;

public class QueueJobStatusDecisionEngine(EmailProviderStore emailProviderStore)
{
    public QueueStatus GetNewJobStatus(string jobType)
    {
        return jobType switch
        {
            EmailQueueJobType.Value => GetNewEmailJobStatus(),
            _ => QueueStatus.PendingStatus
        };
    }

    private QueueStatus GetNewEmailJobStatus()
    {
        return emailProviderStore.IsEmailSenderAvailable
            ? QueueStatus.PendingStatus
            : QueueStatus.BlockedStatus;
    }
}