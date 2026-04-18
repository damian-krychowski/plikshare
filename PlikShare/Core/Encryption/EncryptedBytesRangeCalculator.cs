using PlikShare.Files.PreSignedLinks.RangeRequests;

namespace PlikShare.Core.Encryption;

public class EncryptedBytesRangeCalculator(
    long headerSize,
    long firstSegmentCiphertextSize,
    long nextSegmentsCiphertextSize,
    long tagSize)
{
    private readonly long _firstSegmentFullSize = headerSize + firstSegmentCiphertextSize + tagSize;
    private readonly long _nextSegmentFullSize = nextSegmentsCiphertextSize + tagSize;

    private long FirstSegmentTagStartIndex => headerSize + firstSegmentCiphertextSize;

    public EncryptedBytesRange FromUnencryptedRange(
        BytesRange unencryptedRange, 
        long unencryptedFileSize)
    {
        var startIndex = FindEncryptedIndex(unencryptedRange.Start);
        var endIndex = FindEncryptedIndex(unencryptedRange.End);
        var encryptedFileLastByteIndex = FindEncryptedIndex(unencryptedFileSize - 1) + tagSize;

        var firstSegment = FindSegment(startIndex, encryptedFileLastByteIndex);
        var lastSegment = FindSegment(endIndex, encryptedFileLastByteIndex);

        return new EncryptedBytesRange(
            FirstSegment: firstSegment,
            LastSegment: lastSegment,
            FirstSegmentReadStart: (int)(startIndex - firstSegment.Start),
            LastSegmentReadEnd: (int)(endIndex - lastSegment.Start));
    }

    public EncryptedBytesRange.Segment FindSegment(long encryptedIndex, long encryptedFileLastByteIndex)
    {
        if (encryptedIndex < 0)
            throw new ArgumentOutOfRangeException(
                nameof(encryptedIndex),
                encryptedIndex,
                "Encrypted index cannot be negative.");

        if (encryptedIndex < headerSize)
            throw new ArgumentOutOfRangeException(
                nameof(encryptedIndex),
                encryptedIndex,
                $"Encrypted index {encryptedIndex} falls within the header region [0..{headerSize - 1}]. " +
                $"It must point to a ciphertext byte.");

        if (encryptedIndex > encryptedFileLastByteIndex)
            throw new ArgumentOutOfRangeException(
                nameof(encryptedIndex),
                encryptedIndex,
                $"Encrypted index {encryptedIndex} exceeds the last byte of the encrypted file ({encryptedFileLastByteIndex}).");

        if (encryptedIndex < FirstSegmentTagStartIndex)
        {
            return new EncryptedBytesRange.Segment(
                Number: 0,
                Start: headerSize,
                End: Math.Min(
                    _firstSegmentFullSize - 1,
                    encryptedFileLastByteIndex));
        }

        if (encryptedIndex < _firstSegmentFullSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(encryptedIndex),
                encryptedIndex,
                $"Encrypted index {encryptedIndex} falls within the tag region of segment 0 " +
                $"[{FirstSegmentTagStartIndex}..{_firstSegmentFullSize - 1}].");
        }

        var remainingBytes = encryptedIndex - _firstSegmentFullSize;
        var fullSegments = remainingBytes / _nextSegmentFullSize;
        var segmentNumber = (int) fullSegments + 1;
        var segmentStart = _firstSegmentFullSize + (segmentNumber - 1) * _nextSegmentFullSize;

        var indexInSegment = encryptedIndex - segmentStart;

        if (indexInSegment >= nextSegmentsCiphertextSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(encryptedIndex),
                encryptedIndex,
                $"Encrypted index {encryptedIndex} falls within the tag region of segment {segmentNumber} " +
                $"[{segmentStart + nextSegmentsCiphertextSize}..{segmentStart + _nextSegmentFullSize - 1}].");
        }

        return new EncryptedBytesRange.Segment(
            Number: segmentNumber,
            Start: segmentStart,
            End: Math.Min(
                segmentStart + _nextSegmentFullSize - 1,
                encryptedFileLastByteIndex));
    }

    public long FindEncryptedIndex(long unencryptedIndex)
    {
        var offset = CalculateOffset(unencryptedIndex);

        return offset + unencryptedIndex;
    }

    private long CalculateOffset(long unencryptedIndex)
    {
        var offset = headerSize;

        if (unencryptedIndex < firstSegmentCiphertextSize)
        {
            return offset;
        }

        offset += tagSize;

        var remainingLength = unencryptedIndex - firstSegmentCiphertextSize;

        var fullNextSegments = remainingLength / nextSegmentsCiphertextSize;

        offset += fullNextSegments * tagSize;

        return offset;
    }
}