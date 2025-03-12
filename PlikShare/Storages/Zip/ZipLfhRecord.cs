namespace PlikShare.Storages.Zip;

public class ZipLfhRecord
{
    public const int MinimumSize = 30;
    public const uint SignatureValue = 0x04034b50;

    public required ushort VersionNeededToExtract { get; init; }
    public required ushort GeneralPurposeBitFlag { get; init; }
    public required ushort CompressionMethod { get; init; }
    public required ushort LastModificationTime { get; init; }
    public required ushort LastModificationDate { get; init; }
    public required uint Crc32OfUncompressedData { get; init; }
    public required uint CompressedSize { get; init; }
    public required uint UncompressedSize { get; init; }
    public required ushort FileNameLength { get; init; }
    public required ushort ExtraFieldLength { get; init; }
    
    //we dont need to read those values form LFH records as the name we already now, and extra field is not needed
    //public required string FileName { get; init; }
    //public required byte[] ExtraField { get; init; }
}