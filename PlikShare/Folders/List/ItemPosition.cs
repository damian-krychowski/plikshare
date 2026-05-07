namespace PlikShare.Folders.List;

public static class ItemPosition
{
    public const long Step = 1024;

    public static (long Position, long MaxPosition) Calculate(long? storedPosition, long maxPosition)
    {
        if (storedPosition is null)
        {
            var newMax = maxPosition + Step;
            return (newMax, newMax);
        }

        var position = storedPosition.Value;
        return (position, position > maxPosition ? position : maxPosition);
    }
}
