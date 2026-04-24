using System.Numerics;
using System.Text;

namespace PlikShare.Core.Utils;

public static class Base62Encoding
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

    /// <summary>
    /// Encodes an arbitrary-length byte array to Base62 in a length-preserving way, so
    /// the mapping from byte arrays to strings is injective (two different byte arrays
    /// always produce two different strings — including arrays that differ only in
    /// high-order zero bytes).
    ///
    /// Bytes are interpreted as a little-endian unsigned integer, so any zero bytes
    /// sitting at the END of the array (high-order side in LE) would otherwise be lost
    /// when the value is encoded. To preserve them we apply the Base58 convention:
    /// strip trailing zero bytes, encode the remainder as a Base62 integer, and prepend
    /// one <c>'0'</c> marker for each stripped byte. A fully-zero array of length N
    /// encodes to N copies of <c>'0'</c>.
    ///
    /// Non-zero integer encodings never begin with <c>'0'</c> (the most-significant
    /// digit is always between 1 and 61), so the leading-marker run is unambiguous.
    /// </summary>
    public static string ToBase62(this ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        // In LE storage, high-order zero bytes sit at the end; count and strip them
        // to form the "significant" portion.
        var highZeroByteCount = 0;
        while (highZeroByteCount < bytes.Length
               && bytes[bytes.Length - 1 - highZeroByteCount] == 0)
        {
            highZeroByteCount++;
        }

        if (highZeroByteCount == bytes.Length)
        {
            // All-zero input — one '0' marker per byte, length preserved.
            return new string(Characters[0], bytes.Length);
        }

        var significant = bytes[..(bytes.Length - highZeroByteCount)];

        var numericValue = new BigInteger(
            value: significant,
            isUnsigned: true,
            isBigEndian: false);

        // ceil(N_bits * log(2) / log(62)) with a safety char.
        var integerCharsMax = (int)Math.Ceiling(significant.Length * 8 * 0.16796) + 1;

        Span<char> buffer = stackalloc char[integerCharsMax];
        var position = integerCharsMax;

        while (numericValue > 0)
        {
            numericValue = BigInteger.DivRem(numericValue, Base, out var remainder);
            buffer[--position] = Characters[(int)remainder];
        }

        var integerPart = buffer[position..];

        if (highZeroByteCount == 0)
            return new string(integerPart);

        return string.Concat(
            new string(Characters[0], highZeroByteCount),
            integerPart);
    }

    public static string ToBase62(this byte[] bytes) => ToBase62((ReadOnlySpan<byte>)bytes);

    /// <summary>
    /// Inverse of <see cref="ToBase62(ReadOnlySpan{byte})"/>. Recovers the original byte
    /// array including high-order zero bytes that the integer-only encoding would have
    /// dropped. Throws <see cref="FormatException"/> on non-Base62 characters.
    /// </summary>
    public static byte[] FromBase62ToBytes(string base62String)
    {
        ArgumentNullException.ThrowIfNull(base62String);

        if (!TryDecode(base62String, out var result))
        {
            throw new FormatException(
                "Input contains one or more non-Base62 characters.");
        }

        return result;
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="FromBase62ToBytes"/>. Returns <c>false</c>
    /// if <paramref name="base62String"/> is <c>null</c> or contains any character outside
    /// the Base62 alphabet; otherwise returns <c>true</c> and sets <paramref name="result"/>
    /// to the decoded bytes.
    /// </summary>
    public static bool TryFromBase62ToBytes(string? base62String, out byte[] result)
    {
        if (base62String is null)
        {
            result = [];
            return false;
        }

        return TryDecode(base62String, out result);
    }

    private static bool TryDecode(string base62String, out byte[] result)
    {
        if (base62String.Length == 0)
        {
            result = [];
            return true;
        }

        var leadingZeroMarkers = 0;
        while (leadingZeroMarkers < base62String.Length
               && base62String[leadingZeroMarkers] == Characters[0])
        {
            leadingZeroMarkers++;
        }

        if (leadingZeroMarkers == base62String.Length)
        {
            // All '0' — all-zero input of this exact length.
            result = new byte[base62String.Length];
            return true;
        }

        BigInteger numericValue = 0;
        for (var i = leadingZeroMarkers; i < base62String.Length; i++)
        {
            var digit = Characters.IndexOf(base62String[i]);
            if (digit < 0)
            {
                result = [];
                return false;
            }

            numericValue *= Base;
            numericValue += digit;
        }

        var significant = numericValue.ToByteArray(
            isUnsigned: true,
            isBigEndian: false);

        var bytes = new byte[significant.Length + leadingZeroMarkers];
        significant.CopyTo(bytes, 0);

        result = bytes;
        return true;
    }
}