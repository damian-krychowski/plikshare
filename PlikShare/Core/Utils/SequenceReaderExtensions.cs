using System.Buffers;

namespace PlikShare.Core.Utils;

public static class SequenceReaderExtensions
{
    public static bool TryReadUInt64LittleEndian(ref this SequenceReader<byte> reader, out ulong value)
    {
        if (reader.TryReadLittleEndian(out long longValue))
        {
            value = (ulong)longValue;
            return true;
        }

        value = default;
        return false;
    }

    public static ulong ReadUInt64LittleEndian(ref this SequenceReader<byte> reader)
    {
        if (!TryReadUInt64LittleEndian(ref reader, out var value))
            throw new NotEnoughDataInSequenceException();

        return value;
    }

    public static bool TryReadUInt32LittleEndian(ref this SequenceReader<byte> reader, out uint value)
    {
        if (reader.TryReadLittleEndian(out int intValue))
        {
            value = (uint) intValue;
            return true;
        }

        value = default;
        return false;
    }

    public static uint ReadUInt32LittleEndian(ref this SequenceReader<byte> reader)
    {
        if (!TryReadUInt32LittleEndian(ref reader, out var value))
            throw new NotEnoughDataInSequenceException();

        return value;
    }

    public static bool TryReadUInt16LittleEndian(ref this SequenceReader<byte> reader, out ushort value)
    {
        if (reader.TryReadLittleEndian(out short shortValue))
        {
            value = (ushort)shortValue;
            return true;
        }

        value = default;
        return false;
    }

    public static ushort ReadUInt16LittleEndian(ref this SequenceReader<byte> reader)
    {
        if (!TryReadUInt16LittleEndian(ref reader, out var value))
            throw new NotEnoughDataInSequenceException();

        return value;
    }
}

public class NotEnoughDataInSequenceException : Exception
{

}