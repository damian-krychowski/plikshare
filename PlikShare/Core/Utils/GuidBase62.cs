using System.Numerics;
using System.Text;

namespace PlikShare.Core.Utils;

public static class GuidBase62
{
    private const uint Base = 62;
    private const ulong Multiplier = 297528130221121800;
    private const ulong Constant = 16;
    private const string Characters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Buffer = "0000000000000000000000";

    private static void DivideWithRemainder(ulong number, out ulong quotient, out uint remainder)
    {
        quotient = number / Base;
        remainder = (uint)(number - Base * quotient);
    }

    private static void DivideWithRemainder(
        ulong upperPart, 
        ulong lowerPart, 
        out ulong upperQuotient,
        out ulong lowerQuotient, 
        out uint remainder)
    {
        DivideWithRemainder(upperPart, out upperQuotient, out var upperRemainder);
        DivideWithRemainder(lowerPart, out lowerQuotient, out var lowerRemainder);

        lowerRemainder += (uint)(upperRemainder * Constant);

        DivideWithRemainder(lowerRemainder, out var lowerRemainderQuotient, out remainder);
        
        lowerQuotient += upperRemainder * Multiplier;
        lowerQuotient += lowerRemainderQuotient;
    }

    public static string ToBase62(this Guid guid)
    {
        if (!BitConverter.IsLittleEndian)
            throw new NotSupportedException("Only little endian is supported.");

        Span<byte> guidBytes = stackalloc byte[16];

        guid.TryWriteBytes(guidBytes);
        
        var lowerValue = BitConverter.ToUInt64(
            guidBytes.Slice(0, 8));

        var upperValue = BitConverter.ToUInt64(
            guidBytes.Slice(8));

        var builder = new StringBuilder(
            Buffer);

        var position = Buffer.Length;
        uint remainderValue;

        while (upperValue != 0)
        {
            DivideWithRemainder(upperValue, lowerValue, out upperValue, out lowerValue, out remainderValue);
            builder[--position] = Characters[(int)remainderValue];
        }

        do
        {
            DivideWithRemainder(lowerValue, out lowerValue, out remainderValue);
            builder[--position] = Characters[(int)remainderValue];
        } while (lowerValue != 0);

        return builder.ToString(
            position,
            Buffer.Length - position);
    }
    
    public static Guid FromBase62ToGuid(string base62String)
    {
        BigInteger numericValue = 0;
        for (var i = 0; i < base62String.Length; i++)
        {
            numericValue *= Base;
            numericValue += Characters.IndexOf(base62String[i]);
        }
        
        Span<byte> guidBytes = stackalloc byte[16];
        guidBytes.Clear();

        numericValue.TryWriteBytes(
            guidBytes,
            out _,
            isUnsigned: true,
            isBigEndian: false);
        
        return new Guid(guidBytes);
    }
}