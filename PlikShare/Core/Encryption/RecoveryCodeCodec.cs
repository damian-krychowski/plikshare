using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Encodes 32 bytes of entropy as a BIP-39 24-word recovery phrase and decodes it back.
///
/// BIP-39 (256-bit variant):
///   1. Append 8-bit checksum = SHA-256(entropy)[0] → 264 bits.
///   2. Split into 24 groups of 11 bits.
///   3. Each group is an index into the 2048-word wordlist.
///
/// Decode verifies the checksum in constant time to reject typos before any
/// cryptographic operation is attempted on the seed.
/// </summary>
public static class RecoveryCodeCodec
{
    public const int EntropyBytes = 32;
    public const int WordCount = 24;
    private const int BitsPerWord = 11;
    private const int ChecksumBits = 8;

    public static string Encode(ReadOnlySpan<byte> entropy)
    {
        if (entropy.Length != EntropyBytes)
            throw new ArgumentException(
                $"Entropy must be exactly {EntropyBytes} bytes, got {entropy.Length}.", nameof(entropy));

        Span<byte> combined = stackalloc byte[EntropyBytes + 1];
        entropy.CopyTo(combined);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(entropy, hash);
        combined[EntropyBytes] = hash[0];

        Span<int> indices = stackalloc int[WordCount];
        ExtractIndices(combined, indices);

        var words = new string[WordCount];
        for (var i = 0; i < WordCount; i++)
            words[i] = Bip39Wordlist.GetWord(indices[i]);

        return string.Join(' ', words);
    }

    public static string[] Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var lowered = raw.ToLowerInvariant();

        Span<char> buffer = stackalloc char[lowered.Length];
        for (var i = 0; i < lowered.Length; i++)
        {
            var c = lowered[i];
            buffer[i] = char.IsLetter(c) ? c : ' ';
        }

        return new string(buffer)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static DecodeResult TryDecode(string code, out byte[] entropy)
    {
        entropy = [];

        var words = Normalize(code);

        if (words.Length != WordCount)
            return DecodeResult.WrongWordCount;

        Span<int> indices = stackalloc int[WordCount];
        for (var i = 0; i < WordCount; i++)
        {
            if (!Bip39Wordlist.TryGetIndex(words[i], out var idx))
                return DecodeResult.UnknownWord;

            indices[i] = idx;
        }

        Span<byte> combined = stackalloc byte[EntropyBytes + 1];
        AssembleBytes(indices, combined);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(combined[..EntropyBytes], hash);

        var checksumMatch = CryptographicOperations.FixedTimeEquals(
            combined.Slice(EntropyBytes, 1),
            hash[..1]);

        if (!checksumMatch)
            return DecodeResult.InvalidChecksum;

        entropy = combined[..EntropyBytes].ToArray();
        return DecodeResult.Ok;
    }

    private static void ExtractIndices(ReadOnlySpan<byte> combined, Span<int> indices)
    {
        uint buffer = 0;
        var bufferBits = 0;
        var wordIdx = 0;

        foreach (var b in combined)
        {
            buffer = (buffer << 8) | b;
            bufferBits += 8;

            while (bufferBits >= BitsPerWord)
            {
                bufferBits -= BitsPerWord;
                indices[wordIdx++] = (int)((buffer >> bufferBits) & 0x7FF);
            }
        }
    }

    private static void AssembleBytes(ReadOnlySpan<int> indices, Span<byte> combined)
    {
        uint buffer = 0;
        var bufferBits = 0;
        var byteIdx = 0;

        foreach (var idx in indices)
        {
            buffer = (buffer << BitsPerWord) | (uint)idx;
            bufferBits += BitsPerWord;

            while (bufferBits >= 8)
            {
                bufferBits -= 8;
                combined[byteIdx++] = (byte)((buffer >> bufferBits) & 0xFF);
            }
        }
    }

    public enum DecodeResult
    {
        Ok,
        WrongWordCount,
        UnknownWord,
        InvalidChecksum
    }
}
