using System.Buffers.Binary;

namespace PlikShare.Storages.Zip;

public class Zip64EocdRecord
{
    public const uint SignatureValue = 0x06064b50;
    public const int MinimumSize = 56;

    public required long SizeOfZip64EocdRecord { get; init; }  // Total size excluding signature and this field
    public required ushort VersionMadeBy { get; init; }
    public required ushort VersionNeededToExtract { get; init; }
    public required uint DiskNumber { get; init; }
    public required uint DiskWithCentralDirectoryStart { get; init; }
    public required long NumberOfEntriesOnDisk { get; init; }
    public required long TotalNumberOfEntries { get; init; }
    public required long SizeOfCentralDirectory { get; init; }
    public required long OffsetToCentralDirectory { get; init; }

    // Variable length extensible data sector follows but we can ignore it
}

public static class Zip64EocdRecordSerializer
{
    public static string Serialize(Zip64EocdRecord record)
    {
        var buffer = new byte[Zip64EocdRecord.MinimumSize];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], Zip64EocdRecord.SignatureValue);
        BinaryPrimitives.WriteInt64LittleEndian(span[4..], record.SizeOfZip64EocdRecord);
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], record.VersionMadeBy);
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..], record.VersionNeededToExtract);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], record.DiskNumber);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], record.DiskWithCentralDirectoryStart);
        BinaryPrimitives.WriteInt64LittleEndian(span[24..], record.NumberOfEntriesOnDisk);
        BinaryPrimitives.WriteInt64LittleEndian(span[32..], record.TotalNumberOfEntries);
        BinaryPrimitives.WriteInt64LittleEndian(span[40..], record.SizeOfCentralDirectory);
        BinaryPrimitives.WriteInt64LittleEndian(span[48..], record.OffsetToCentralDirectory);

        return Convert.ToBase64String(buffer);
    }

    public static Zip64EocdRecord Deserialize(string base64)
    {
        var data = Convert.FromBase64String(base64);
        return Deserialize(data);
    }


    private static Zip64EocdRecord Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < Zip64EocdRecord.MinimumSize)
            throw new ArgumentException("Data too short for ZIP64 EOCD record");

        if (BinaryPrimitives.ReadUInt32LittleEndian(data) != Zip64EocdRecord.SignatureValue)
            throw new ArgumentException("Invalid ZIP64 EOCD signature");

        return new Zip64EocdRecord
        {
            SizeOfZip64EocdRecord = BinaryPrimitives.ReadInt64LittleEndian(data[4..]),
            VersionMadeBy = BinaryPrimitives.ReadUInt16LittleEndian(data[12..]),
            VersionNeededToExtract = BinaryPrimitives.ReadUInt16LittleEndian(data[14..]),
            DiskNumber = BinaryPrimitives.ReadUInt32LittleEndian(data[16..]),
            DiskWithCentralDirectoryStart = BinaryPrimitives.ReadUInt32LittleEndian(data[20..]),
            NumberOfEntriesOnDisk = BinaryPrimitives.ReadInt64LittleEndian(data[24..]),
            TotalNumberOfEntries = BinaryPrimitives.ReadInt64LittleEndian(data[32..]),
            SizeOfCentralDirectory = BinaryPrimitives.ReadInt64LittleEndian(data[40..]),
            OffsetToCentralDirectory = BinaryPrimitives.ReadInt64LittleEndian(data[48..])
        };
    }
}