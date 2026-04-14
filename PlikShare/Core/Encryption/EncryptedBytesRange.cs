namespace PlikShare.Core.Encryption;

public record EncryptedBytesRange(
    EncryptedBytesRange.Segment FirstSegment,
    EncryptedBytesRange.Segment LastSegment,
    int FirstSegmentReadOffset,
    int LastSegmentReadOffset)
{
    public readonly record struct Segment(
        int Number,
        long Start,
        long End);
}