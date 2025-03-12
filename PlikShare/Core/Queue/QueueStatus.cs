namespace PlikShare.Core.Queue;

public class QueueStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
    
    public string Value { get; }
    
    private QueueStatus(string value)
    {
        Value = value;
    }

    public static QueueStatus PendingStatus { get; } =  new(Pending);
    public static QueueStatus ProcessingStatus{ get; } = new(Processing);
    public static QueueStatus FailedStatus { get; } = new(Failed);
    public static QueueStatus BlockedStatus { get; } = new(Blocked);
}