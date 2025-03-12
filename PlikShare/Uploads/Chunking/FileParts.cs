using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;

namespace PlikShare.Uploads.Chunking;

public static class FileParts
{
    public const int UnencryptedFilePartSize = 10 * SizeInBytes.Mb;

    public static int GetTotalNumberOfParts(long fileSizeInBytes, StorageEncryptionType storageEncryptionType)
    {
        return storageEncryptionType switch
        {
            StorageEncryptionType.None => (int)Math.Ceiling((double)fileSizeInBytes / UnencryptedFilePartSize),

            StorageEncryptionType.Managed => Aes256GcmStreaming.GetExpectedPartsCount(fileSizeInBytes),

            _ => throw new ArgumentOutOfRangeException(nameof(storageEncryptionType), storageEncryptionType, null)
        };
    }
    
    public static bool IsPartNumberAllowed(
        long fileSizeInBytes, 
        int partNumber,
        StorageEncryptionType storageEncryptionType)
    {
        var totalNumberOfParts = GetTotalNumberOfParts(
            fileSizeInBytes: fileSizeInBytes,
            storageEncryptionType: storageEncryptionType);

        return partNumber > 0 && partNumber <= totalNumberOfParts;
    }

    public static int GetPartSizeInBytes(
        long fileSizeInBytes,
        int partNumber,
        StorageEncryptionType storageEncryptionType)
    {
        var (startByte, endByte) = GetPartByteRange(
            fileSizeInBytes: fileSizeInBytes, 
            partNumber: partNumber, 
            storageEncryptionType: storageEncryptionType);

        return (int) (endByte - startByte + 1);
    }

    public static (long StartByte, long EndByte) GetPartByteRange(
        long fileSizeInBytes,
        int partNumber,
        StorageEncryptionType storageEncryptionType)
    {
        if (!IsPartNumberAllowed(fileSizeInBytes, partNumber, storageEncryptionType))
        {
            throw new ArgumentException($"Part number {partNumber} is not allowed for file size {fileSizeInBytes} and encryption mode {storageEncryptionType}");
        }

        return storageEncryptionType switch
        {
            StorageEncryptionType.None => CalculateUnencryptedPartByteRange(fileSizeInBytes, partNumber),
            StorageEncryptionType.Managed => CalculateEncryptedPartByteRange(fileSizeInBytes, partNumber),
            _ => throw new ArgumentOutOfRangeException(nameof(storageEncryptionType), storageEncryptionType, null)
        };
    }

    private static (long StartByte, long EndByte) CalculateUnencryptedPartByteRange(long fileSizeInBytes, int partNumber)
    {
        var startByte = (partNumber - 1) * (long)UnencryptedFilePartSize;
        var endByte = Math.Min(startByte + UnencryptedFilePartSize - 1, fileSizeInBytes - 1);
        return (startByte, endByte);
    }

    private static (long StartByte, long EndByte) CalculateEncryptedPartByteRange(long fileSizeInBytes, int partNumber)
    {
        if (partNumber == 1)
        {
            return (0, Math.Min(Aes256GcmStreaming.FirstFilePartSizeInBytes - 1, fileSizeInBytes - 1));
        }

        var startByte = Aes256GcmStreaming.FirstFilePartSizeInBytes +
                        (partNumber - 2) * (long)Aes256GcmStreaming.FilePartSizeInBytes;

        var endByte = Math.Min(
            startByte + Aes256GcmStreaming.FilePartSizeInBytes - 1,
            fileSizeInBytes - 1);

        return (startByte, endByte);
    }
}
