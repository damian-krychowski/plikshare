using System.Buffers.Binary;
using PlikShare.Files.PreSignedLinks.RangeRequests;

namespace PlikShare.Storages.Zip;

public class ZipEocdRecord
{
    public const int MinimumSize = 22;

    public static readonly byte[] Signature = [0x50, 0x4B, 0x05, 0x06]; //Signature (0x06054b50) - little endian

    public required ushort NumberOfThisDisk { get; init; }
    public required ushort DiskWhereCentralDirectoryStarts { get; init; }
    public required ushort NumbersOfCentralDirectoryRecordsOnThisDisk { get; init; }
    public required ushort TotalNumberOfCentralDirectoryRecords { get; init; }
    public required uint SizeOfCentralDirectoryInBytes { get; init; }
    public required uint OffsetToStartCentralDirectory { get; init; }
    public required ushort CommentLength { get; init; }

    public BytesRange CentralDirectoryBytesRange => new(
        Start: OffsetToStartCentralDirectory,
        End: OffsetToStartCentralDirectory + SizeOfCentralDirectoryInBytes - 1);
}

public static class ZipEocdRecordSerializer
{
    public static string Serialize(ZipEocdRecord record)
    {
        var buffer = new byte[ZipEocdRecord.MinimumSize];
        var span = buffer.AsSpan();

        ZipEocdRecord.Signature.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], record.NumberOfThisDisk);
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], record.DiskWhereCentralDirectoryStarts);
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], record.NumbersOfCentralDirectoryRecordsOnThisDisk);
        BinaryPrimitives.WriteUInt16LittleEndian(span[10..], record.TotalNumberOfCentralDirectoryRecords);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], record.SizeOfCentralDirectoryInBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], record.OffsetToStartCentralDirectory);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], record.CommentLength);

        return Convert.ToBase64String(buffer);
    }

    public static ZipEocdRecord Deserialize(string base64)
    {
        var data = Convert.FromBase64String(base64);
        return Deserialize(data);
    }

    private static ZipEocdRecord Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < ZipEocdRecord.MinimumSize)
            throw new ArgumentException("Data too short for EOCD record");

        if (!data[..4].SequenceEqual(ZipEocdRecord.Signature))
            throw new ArgumentException("Invalid EOCD signature");

        return new ZipEocdRecord
        {
            NumberOfThisDisk = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]),
            DiskWhereCentralDirectoryStarts = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]),
            NumbersOfCentralDirectoryRecordsOnThisDisk = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]),
            TotalNumberOfCentralDirectoryRecords = BinaryPrimitives.ReadUInt16LittleEndian(data[10..]),
            SizeOfCentralDirectoryInBytes = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]),
            OffsetToStartCentralDirectory = BinaryPrimitives.ReadUInt32LittleEndian(data[16..]),
            CommentLength = BinaryPrimitives.ReadUInt16LittleEndian(data[20..])
        };
    }
}