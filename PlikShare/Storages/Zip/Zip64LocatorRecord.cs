using System.Buffers.Binary;

namespace PlikShare.Storages.Zip;

public class Zip64LocatorRecord
{
    public static byte[] Signature = [0x50, 0x4b, 0x06, 0x07];
    public const uint SignatureValue = 0x07064b50;  // PK\06\07
    public const int Size = 20;  // Fixed size of 20 bytes

    public required uint DiskWithZip64Eocd { get; init; }
    public required long Zip64EocdOffset { get; init; }  
    public required uint TotalNumberOfDisks { get; init; }
}

public static class Zip64LocatorRecordSerializer
{
    public static byte[] Serialize(Zip64LocatorRecord record)
    {
        var buffer = new byte[Zip64LocatorRecord.Size];
        var span = buffer.AsSpan();

        Zip64LocatorRecord.Signature.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], record.DiskWithZip64Eocd);
        BinaryPrimitives.WriteInt64LittleEndian(span[8..], record.Zip64EocdOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], record.TotalNumberOfDisks);

        return buffer;
    }

    public static Zip64LocatorRecord Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < Zip64LocatorRecord.Size)
            throw new ArgumentException("Data too short for ZIP64 locator record");

        if (!data[..4].SequenceEqual(Zip64LocatorRecord.Signature))
            throw new ArgumentException("Invalid ZIP64 locator signature");

        return new Zip64LocatorRecord
        {
            DiskWithZip64Eocd = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]),
            Zip64EocdOffset = BinaryPrimitives.ReadInt64LittleEndian(data[8..]),
            TotalNumberOfDisks = BinaryPrimitives.ReadUInt32LittleEndian(data[16..])
        };
    }
}