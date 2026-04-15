using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;

namespace PlikShare.Uploads.Chunking;

public static class FileParts
{
    public const int UnencryptedFilePartSize = 10 * SizeInBytes.Mb;

    // Managed encryption keeps the flat V1 frame (no hierarchical chain).
    // Full encryption uses the V2 frame where per-file chain depth drives the header size.

    public static int GetTotalNumberOfParts(
        long fileSizeInBytes,
        StorageEncryptionType storageEncryptionType,
        int ikmChainStepsCount)
    {
        return storageEncryptionType switch
        {
            StorageEncryptionType.None => (int)Math.Ceiling((double)fileSizeInBytes / UnencryptedFilePartSize),

            StorageEncryptionType.Managed => Aes256GcmStreamingV1.GetExpectedPartsCount(fileSizeInBytes),

            StorageEncryptionType.Full => Aes256GcmStreamingV2.GetExpectedPartsCount(fileSizeInBytes, ikmChainStepsCount),

            _ => throw new ArgumentOutOfRangeException(nameof(storageEncryptionType), storageEncryptionType, null)
        };
    }

    public static bool IsPartNumberAllowed(
        long fileSizeInBytes,
        int partNumber,
        StorageEncryptionType storageEncryptionType,
        int ikmChainStepsCount)
    {
        var totalNumberOfParts = GetTotalNumberOfParts(
            fileSizeInBytes: fileSizeInBytes,
            storageEncryptionType: storageEncryptionType,
            ikmChainStepsCount: ikmChainStepsCount);

        return partNumber > 0 && partNumber <= totalNumberOfParts;
    }

    public static int GetPartSizeInBytes(
        long fileSizeInBytes,
        int partNumber,
        StorageEncryptionType storageEncryptionType,
        int ikmChainStepsCount)
    {
        var (startByte, endByte) = GetPartByteRange(
            fileSizeInBytes: fileSizeInBytes,
            partNumber: partNumber,
            storageEncryptionType: storageEncryptionType,
            ikmChainStepsCount: ikmChainStepsCount);

        return (int) (endByte - startByte + 1);
    }

    public static (long StartByte, long EndByte) GetPartByteRange(
        long fileSizeInBytes,
        int partNumber,
        StorageEncryptionType storageEncryptionType,
        int ikmChainStepsCount)
    {
        if (!IsPartNumberAllowed(fileSizeInBytes, partNumber, storageEncryptionType, ikmChainStepsCount))
        {
            throw new ArgumentException($"Part number {partNumber} is not allowed for file size {fileSizeInBytes} and encryption mode {storageEncryptionType}");
        }

        return storageEncryptionType switch
        {
            StorageEncryptionType.None => CalculateUnencryptedPartByteRange(
                fileSizeInBytes,
                partNumber),

            StorageEncryptionType.Managed => CalculateV1EncryptedPartByteRange(
                fileSizeInBytes,
                partNumber),

            StorageEncryptionType.Full => CalculateV2EncryptedPartByteRange(
                fileSizeInBytes,
                partNumber,
                ikmChainStepsCount),

            _ => throw new ArgumentOutOfRangeException(nameof(storageEncryptionType), storageEncryptionType, null)
        };
    }

    private static (long StartByte, long EndByte) CalculateUnencryptedPartByteRange(long fileSizeInBytes, int partNumber)
    {
        var startByte = (partNumber - 1) * (long)UnencryptedFilePartSize;
        var endByte = Math.Min(startByte + UnencryptedFilePartSize - 1, fileSizeInBytes - 1);
        return (startByte, endByte);
    }

    private static (long StartByte, long EndByte) CalculateV1EncryptedPartByteRange(
        long fileSizeInBytes,
        int partNumber)
    {
        if (partNumber == 1)
        {
            return (0, Math.Min(Aes256GcmStreamingV1.FirstFilePartSizeInBytes - 1, fileSizeInBytes - 1));
        }

        var startByte = Aes256GcmStreamingV1.FirstFilePartSizeInBytes +
                        (partNumber - 2) * (long)Aes256GcmStreamingV1.FilePartSizeInBytes;

        var endByte = Math.Min(
            startByte + Aes256GcmStreamingV1.FilePartSizeInBytes - 1,
            fileSizeInBytes - 1);

        return (startByte, endByte);
    }

    private static (long StartByte, long EndByte) CalculateV2EncryptedPartByteRange(
        long fileSizeInBytes,
        int partNumber,
        int ikmChainStepsCount)
    {
        var firstFilePartSize = Aes256GcmStreamingV2.GetFirstFilePartSizeInBytes(ikmChainStepsCount);

        if (partNumber == 1)
        {
            return (0, Math.Min(firstFilePartSize - 1, fileSizeInBytes - 1));
        }

        var startByte = firstFilePartSize +
                        (partNumber - 2) * Aes256GcmStreamingV2.FilePartSizeInBytes;

        var endByte = Math.Min(
            startByte + Aes256GcmStreamingV2.FilePartSizeInBytes - 1,
            fileSizeInBytes - 1);

        return (startByte, endByte);
    }
}
