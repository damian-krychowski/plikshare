namespace PlikShare.Storages.Zip;

public class Zip64ExtraField
{
    public const ushort HeaderId = 0x0001;  // Identifier for ZIP64 extended information

    public long? UncompressedSize { get; init; }
    public long? CompressedSize { get; init; }
    public long? LocalHeaderOffset { get; init; }
    public uint? DiskStart { get; init; }
}