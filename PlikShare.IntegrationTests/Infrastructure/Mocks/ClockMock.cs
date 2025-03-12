using PlikShare.Core.Clock;

namespace PlikShare.IntegrationTests.Infrastructure.Mocks;

public class ClockMock : IClock
{
    private DateTimeOffset? _utcNow;
    
    public void CurrentTime(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public void SetToNow()
    {
        _utcNow = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset UtcNow => _utcNow ?? DateTimeOffset.UtcNow;
    public DateTime Now => _utcNow?.DateTime ?? DateTime.Now;
}