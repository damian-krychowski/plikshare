namespace PlikShare.Files.PreSignedLinks.RangeRequests;

public readonly record struct BytesRange(
    long Start,
    long End)
{
    public long Length => End - Start + 1;
}

public static class FileBytesRange
{
    public static BytesRange Create(long start, long end, long fileSizeInBytes)
    {
        if (fileSizeInBytes <= 0)
            throw new InvalidBytesRangeException($"File size must be positive. Got: {fileSizeInBytes}");

        if (start < 0)
            throw new InvalidBytesRangeException($"Start position cannot be negative. Got: {start}");

        if (end < 0)
            throw new InvalidBytesRangeException($"End position cannot be negative. Got: {end}");

        if (start >= fileSizeInBytes)
            throw new InvalidBytesRangeException($"Start position ({start}) cannot be greater than or equal to file size ({fileSizeInBytes})");

        if (start > end)
            throw new InvalidBytesRangeException($"Start position ({start}) cannot be greater than end position ({end})");

        return new BytesRange(
            Start: start,
            End: Math.Min(end, fileSizeInBytes - 1));
    }
}

public class InvalidBytesRangeException(string message) : Exception(message);
