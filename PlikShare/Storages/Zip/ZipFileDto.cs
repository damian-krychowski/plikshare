namespace PlikShare.Storages.Zip;

public record ZipFileDto(
    string FileName,
    long CompressedSizeInBytes,
    long SizeInBytes,
    long OffsetToLocalFileHeader,
    ushort FileNameLength,
    ushort CompressionMethod,
    uint IndexInArchive);