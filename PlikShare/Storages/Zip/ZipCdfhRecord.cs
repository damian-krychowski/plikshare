namespace PlikShare.Storages.Zip;

public class ZipCdfhRecord
{
    public const int MinimumSize = 46;
    public const uint SignatureValue = 0x02014b50;

    public required ushort VersionMadeBy { get; init; }
    public required ushort MinimumVersionNeededToExtract { get; init; }
    public required ushort BitFlag { get; init; }
    public required ushort CompressionMethod { get; init; }
    public required ushort FileLastModificationTime { get; init; }
    public required ushort FileLastModificationDate { get; init; }
    public required uint Crc32OfUncompressedData { get; init; }
    public required long CompressedSize { get; init; }  
    public required long UncompressedSize { get; init; } 
    public required ushort FileNameLength { get; init; }
    public required ushort ExtraFieldLength { get; init; }
    public required ushort FileCommentLength { get; init; }
    public required uint DiskNumberWhereFileStarts { get; init; }  
    public required ushort InternalFileAttributes { get; init; }
    public required uint ExternalFileAttributes { get; init; }
    public required long OffsetToLocalFileHeader { get; init; }  
    public required string FileName { get; init; }
    public required byte[] ExtraField { get; init; }
    public required string FileComment { get; init; }
    public required uint IndexInArchive { get; init; }
}