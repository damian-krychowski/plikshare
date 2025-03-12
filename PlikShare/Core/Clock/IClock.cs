namespace PlikShare.Core.Clock;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTime Now { get; }
}