namespace PlikShare.Files.PreSignedLinks.RangeRequests;

/// <summary>
/// Represents a contiguous range of bytes using <b>inclusive</b> start and end positions,
/// matching the semantics of HTTP Range headers (RFC 7233), e.g. <c>Range: bytes=0-99</c>
/// denotes 100 bytes at positions 0 through 99.
/// </summary>
/// <remarks>
/// Both <see cref="Start"/> and <see cref="End"/> are inclusive byte indices.
/// A range covering a single byte at position <c>N</c> is expressed as
/// <c>new BytesRange(N, N)</c> and has <see cref="Length"/> equal to 1.
/// The total number of bytes in the range is <c>End - Start + 1</c>.
/// </remarks>
/// <param name="Start">Inclusive index of the first byte in the range.</param>
/// <param name="End">Inclusive index of the last byte in the range.</param>
public readonly record struct BytesRange(
    long Start,
    long End)
{
    public long Length => End - Start + 1;

    public override string ToString()
    {
        return $"[{Start}-{End}] ({Length} bytes)";
    }
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
