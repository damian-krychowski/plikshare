namespace PlikShare.Core.Utils;

public class TransferSpeed
{
    public static string Format(long bytes, TimeSpan duration)
    {
        // For very quick transfers (less than 100ms), report instantaneous speed
        if (duration.TotalMilliseconds < 100)
        {
            return "< 100ms transfer";
        }

        var speedInBytesPerSec = bytes / duration.TotalSeconds;
        var speedInMBPerSec = speedInBytesPerSec / 1024.0 / 1024.0;

        return $"{speedInMBPerSec:N2} MB/s";
    }
}