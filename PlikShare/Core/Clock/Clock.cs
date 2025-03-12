namespace PlikShare.Core.Clock;

public class Clock: IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTime Now => DateTime.Now;
}