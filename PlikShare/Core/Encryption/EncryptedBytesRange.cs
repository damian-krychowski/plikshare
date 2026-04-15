using PlikShare.Files.PreSignedLinks.RangeRequests;

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

    public BytesRange ToBytesRange()
    {
        return new BytesRange(
            Start: FirstSegment.Start,
            End: LastSegment.End);
    }
}

