using PlikShare.Files.PreSignedLinks.RangeRequests;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Range calculator for V2 frame layout. Configuration constants (segment size, tag size,
/// base header size, step salt size) come through the constructor; the per-file chain length
/// comes through each method as <c>chainStepsCount</c>, because it differs per file.
/// V2 header = baseHeaderSize + chainStepsCount * stepSaltSize bytes.
/// </summary>
public class EncryptedBytesRangeCalculatorV2(
    long segmentSize,
    long tagSize,
    long baseHeaderSize,
    long stepSaltSize)
{
    private long NextSegmentsCiphertextSize => segmentSize - tagSize;
    private long NextSegmentFullSize => segmentSize;

    public long GetHeaderSize(int chainStepsCount)
    {
        if (chainStepsCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(chainStepsCount),
                chainStepsCount,
                "Chain steps count must be non-negative.");

        return baseHeaderSize + chainStepsCount * stepSaltSize;
    }

    public long GetFirstSegmentCiphertextSize(int chainStepsCount)
        => segmentSize - tagSize - GetHeaderSize(chainStepsCount);

    public EncryptedBytesRange FromUnencryptedRange(
        BytesRange unencryptedRange,
        long unencryptedFileSize,
        int chainStepsCount)
    {
        var startIndex = FindEncryptedIndex(unencryptedRange.Start, chainStepsCount);
        var endIndex = FindEncryptedIndex(unencryptedRange.End, chainStepsCount);
        var encryptedFileLastByteIndex = FindEncryptedIndex(unencryptedFileSize - 1, chainStepsCount) + tagSize;

        var firstSegment = FindSegment(startIndex, encryptedFileLastByteIndex, chainStepsCount);
        var lastSegment = FindSegment(endIndex, encryptedFileLastByteIndex, chainStepsCount);

        return new EncryptedBytesRange(
            FirstSegment: firstSegment,
            LastSegment: lastSegment,
            FirstSegmentReadStart: (int)(startIndex - firstSegment.Start),
            LastSegmentReadEnd: (int)(endIndex - lastSegment.Start));
    }

    public EncryptedBytesRange.Segment FindSegment(
        long encryptedIndex,
        long encryptedFileLastByteIndex,
        int chainStepsCount)
    {
        var headerSize = GetHeaderSize(chainStepsCount);
        var firstSegmentCiphertextSize = GetFirstSegmentCiphertextSize(chainStepsCount);
        var firstSegmentFullSize = headerSize + firstSegmentCiphertextSize + tagSize;
        var firstSegmentTagStartIndex = headerSize + firstSegmentCiphertextSize;

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

        if (encryptedIndex < firstSegmentTagStartIndex)
        {
            return new EncryptedBytesRange.Segment(
                Number: 0,
                Start: headerSize,
                End: Math.Min(
                    firstSegmentFullSize - 1,
                    encryptedFileLastByteIndex));
        }

        if (encryptedIndex < firstSegmentFullSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(encryptedIndex),
                encryptedIndex,
                $"Encrypted index {encryptedIndex} falls within the tag region of segment 0 " +
                $"[{firstSegmentTagStartIndex}..{firstSegmentFullSize - 1}].");
        }

        var remainingBytes = encryptedIndex - firstSegmentFullSize;
        var fullSegments = remainingBytes / NextSegmentFullSize;
        var segmentNumber = (int)fullSegments + 1;
        var segmentStart = firstSegmentFullSize + (segmentNumber - 1) * NextSegmentFullSize;

        var indexInSegment = encryptedIndex - segmentStart;

        if (indexInSegment >= NextSegmentsCiphertextSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(encryptedIndex),
                encryptedIndex,
                $"Encrypted index {encryptedIndex} falls within the tag region of segment {segmentNumber} " +
                $"[{segmentStart + NextSegmentsCiphertextSize}..{segmentStart + NextSegmentFullSize - 1}].");
        }

        return new EncryptedBytesRange.Segment(
            Number: segmentNumber,
            Start: segmentStart,
            End: Math.Min(
                segmentStart + NextSegmentFullSize - 1,
                encryptedFileLastByteIndex));
    }

    public long FindEncryptedIndex(long unencryptedIndex, int chainStepsCount)
        => CalculateOffset(unencryptedIndex, chainStepsCount) + unencryptedIndex;

    private long CalculateOffset(long unencryptedIndex, int chainStepsCount)
    {
        var headerSize = GetHeaderSize(chainStepsCount);
        var firstSegmentCiphertextSize = GetFirstSegmentCiphertextSize(chainStepsCount);

        var offset = headerSize;

        if (unencryptedIndex < firstSegmentCiphertextSize)
            return offset;

        offset += tagSize;

        var remainingLength = unencryptedIndex - firstSegmentCiphertextSize;

        var fullNextSegments = remainingLength / NextSegmentsCiphertextSize;

        offset += fullNextSegments * tagSize;

        return offset;
    }
}
