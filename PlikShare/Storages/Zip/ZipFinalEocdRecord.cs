using PlikShare.Files.PreSignedLinks.RangeRequests;

namespace PlikShare.Storages.Zip;

public class ZipFinalEocdRecord(ZipEocdRecord eocd, Zip64EocdRecord? zip64Eocd = null)
{
    public uint DiskNumber { get; } = eocd.NumberOfThisDisk == 0xFFFF
        ? zip64Eocd?.DiskNumber ?? throw new InvalidOperationException("ZIP64 record required but not provided")
        : eocd.NumberOfThisDisk;

    public uint DiskWithCentralDirectoryStart { get; } = eocd.DiskWhereCentralDirectoryStarts == 0xFFFF
        ? zip64Eocd?.DiskWithCentralDirectoryStart ?? throw new InvalidOperationException("ZIP64 record required but not provided")
        : eocd.DiskWhereCentralDirectoryStarts;

    public long NumberOfEntriesOnDisk { get; } = eocd.NumbersOfCentralDirectoryRecordsOnThisDisk == 0xFFFF
        ? zip64Eocd?.NumberOfEntriesOnDisk ?? throw new InvalidOperationException("ZIP64 record required but not provided")
        : eocd.NumbersOfCentralDirectoryRecordsOnThisDisk;

    public long TotalNumberOfEntries { get; } = eocd.TotalNumberOfCentralDirectoryRecords == 0xFFFF
        ? zip64Eocd?.TotalNumberOfEntries ?? throw new InvalidOperationException("ZIP64 record required but not provided")
        : eocd.TotalNumberOfCentralDirectoryRecords;

    public long SizeOfCentralDirectory { get; } = eocd.SizeOfCentralDirectoryInBytes == 0xFFFFFFFF
        ? zip64Eocd?.SizeOfCentralDirectory ?? throw new InvalidOperationException("ZIP64 record required but not provided")
        : eocd.SizeOfCentralDirectoryInBytes;

    public long OffsetToCentralDirectory { get; } = eocd.OffsetToStartCentralDirectory == 0xFFFFFFFF
        ? zip64Eocd?.OffsetToCentralDirectory ?? throw new InvalidOperationException("ZIP64 record required but not provided")
        : eocd.OffsetToStartCentralDirectory;

    public ushort CommentLength { get; } = eocd.CommentLength;

    public BytesRange CentralDirectoryBytesRange => new(
        Start: OffsetToCentralDirectory,
        End: OffsetToCentralDirectory + SizeOfCentralDirectory - 1);
}