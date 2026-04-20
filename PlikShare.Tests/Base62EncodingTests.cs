using System.Security.Cryptography;
using FluentAssertions;
using PlikShare.Core.Utils;

namespace PlikShare.Tests;

public class Base62EncodingTests
{
    [Fact]
    public void can_convert_guid_to_base62_and_back()
    {
        var guid = Guid.NewGuid();
        var base62 = guid.ToBase62();
        var guidConvertedBack = Base62Encoding.FromBase62ToGuid(base62);

        guidConvertedBack.Should().Be(guid);
    }

    // Distinct byte arrays — including those differing ONLY in high-order (LE-trailing)
    // zero bytes — must produce distinct strings. The old integer-only encoder collapsed
    // these to the same output; the new encoder prepends one '0' marker per trailing
    // zero byte (Base58 convention) so length is preserved.
    [Theory]
    [InlineData(new byte[] { 0x01 }, "1")]
    [InlineData(new byte[] { 0x01, 0x00 }, "01")]
    [InlineData(new byte[] { 0x01, 0x00, 0x00 }, "001")]
    [InlineData(new byte[] { 0x01, 0x00, 0x00, 0x00 }, "0001")]
    [InlineData(new byte[] { 0x00 }, "0")]
    [InlineData(new byte[] { 0x00, 0x00 }, "00")]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, "00000000")]
    [InlineData(new byte[] { 0xFF, 0x00, 0x00 }, "0047")]
    [InlineData(new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x00 }, "000047")]
    public void to_base62_preserves_high_order_zero_bytes(byte[] bytes, string expected)
    {
        bytes.ToBase62().Should().Be(expected);
    }

    // Three inputs that would have collided under the old integer-only encoding. This
    // is the specific regression guard: they MUST all be distinct now.
    [Fact]
    public void to_base62_is_injective_over_byte_length()
    {
        var a = new byte[] { 0x01 }.ToBase62();
        var b = new byte[] { 0x01, 0x00 }.ToBase62();
        var c = new byte[] { 0x01, 0x00, 0x00 }.ToBase62();

        new[] { a, b, c }.Distinct().Count().Should().Be(3,
            because: $"got a='{a}', b='{b}', c='{c}' — must all differ");
    }

    // The strongest proof of injectivity: encode → decode → recover the exact original
    // bytes. If this holds for every input (including edge cases with leading/trailing
    // zeros, single bytes, long arrays), the encoder has a left inverse and is therefore
    // injective by definition.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(48)]
    [InlineData(64)]
    public void to_base62_roundtrips_random_bytes_of_length(int byteLength)
    {
        var random = new Random(Seed: 1000 + byteLength);

        for (var i = 0; i < 64; i++)
        {
            var bytes = new byte[byteLength];
            random.NextBytes(bytes);

            var decoded = Base62Encoding.FromBase62ToBytes(bytes.ToBase62());

            decoded.Should().Equal(bytes,
                because: $"roundtrip must recover original bytes for input hex={Convert.ToHexString(bytes)}");
        }
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x00, 0x00 })]
    [InlineData(new byte[] { 0x01, 0x00 })]
    [InlineData(new byte[] { 0xFF, 0x00, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x00, 0x01 })]
    [InlineData(new byte[] { 0x00, 0x00, 0xFF })]
    public void to_base62_roundtrips_edge_cases(byte[] bytes)
    {
        Base62Encoding.FromBase62ToBytes(bytes.ToBase62()).Should().Equal(bytes);
    }

    [Fact]
    public void to_base62_roundtrips_cryptographically_random_32_byte_arrays()
    {
        // The actual invitation-code use case: 32 bytes of cryptographic randomness.
        for (var i = 0; i < 200; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);

            Base62Encoding.FromBase62ToBytes(bytes.ToBase62()).Should().Equal(bytes);
        }
    }

    // Ground-truth vectors computed by an independent external Base62 encoder
    // (dcode.fr/base62-encoding with alphabet "0-9a-zA-Z"), fed the integer value
    // that our little-endian byte array represents. The bytes shown here are the
    // LE-ordered array as stored in C# (same order as input to ToBase62); the
    // external tool was given the big-endian reversal so both interpret the same
    // unsigned integer.
    [Theory]
    [InlineData(new byte[] { 0x39 }, "V")]
    [InlineData(new byte[] { 0x0C, 0x8C }, "9kg")]
    [InlineData(new byte[] { 0x7D, 0x72, 0x47, 0x34, 0x2C, 0xD8, 0x10 }, "lIkXJTl1P")]
    [InlineData(new byte[] { 0x0F, 0x2F, 0x6F, 0x77, 0x0D, 0x65, 0xD6, 0x70 }, "9GD8xOQ4YVN")]
    [InlineData(new byte[] { 0xE5, 0x8E, 0x03, 0x51, 0xD8, 0xAE, 0x8E, 0x4F, 0x6E, 0xAC, 0x34, 0x2F, 0xC2, 0x31, 0xB7, 0xB0 }, "5nsmUMvuILjP23mmbC1rNz")]
    [InlineData(new byte[] { 0x87, 0x16, 0xEB, 0x3F, 0xC1, 0x28, 0x96, 0xB9, 0x62, 0x23, 0x17, 0x74, 0x94, 0x28, 0x77, 0x33, 0xC2 }, "op5JZuerU12Z1bpaHSFR3LN")]
    [InlineData(new byte[] { 0x8E, 0xE8, 0xBA, 0x53, 0xBD, 0xB5, 0x6B, 0x88, 0x24, 0x57, 0x7D, 0x53, 0xEC, 0xC2, 0x8A, 0x70, 0xA6, 0x1C, 0x75, 0x10 }, "2lAy8GDABfLIjIhQVJWFJBmQ7Dw")]
    [InlineData(new byte[] { 0xA1, 0xCD, 0x89, 0x21, 0x6C, 0xA1, 0x6C, 0xFF, 0xCA, 0xEA, 0x49, 0x87, 0x47, 0x7E, 0x86, 0xDB, 0xCC, 0xB9, 0x70, 0x46, 0xFC, 0x2E, 0x18, 0x38, 0x4E, 0x51, 0xD8, 0x20, 0xC5, 0xC3, 0xEF, 0x80 }, "uzDFakRTudVuLTWlgrhj7z3VW2uB9OvgECL7osHHYOZ")]
    [InlineData(new byte[] { 0x05, 0x3A, 0x88, 0xAE, 0x39, 0x96, 0xDE, 0x50, 0xE8, 0x01, 0x86, 0x5B, 0x36, 0x98, 0x65, 0x4E, 0xBF, 0x52, 0x00, 0xA5, 0xFA, 0x09, 0x39, 0xB9, 0x9D, 0x7A, 0x1D, 0x7B, 0x28, 0x2B, 0xF8, 0x23 }, "8wPdRbUWY1VAPp6Meon1niXxYWxhkyJPMivSa0zkKtT")]
    [InlineData(new byte[] { 0x40, 0x41, 0xF3, 0x54, 0x87, 0xD8, 0x6C, 0x66, 0x9F, 0xCC, 0xBF, 0xE0, 0xE7, 0x3D, 0x7E, 0x73, 0x20, 0xAD, 0x0A, 0x75, 0x70, 0x03, 0x24, 0x1E, 0x75, 0x22, 0x10, 0xA9, 0x24, 0x79, 0x8E, 0xF8, 0x6D }, "1JFPYcB6y843qe1p0wjCWDYR2TkTjqzpNbhe5TIhuxzDa")]
    [InlineData(new byte[] { 0x43, 0xF2, 0x7C, 0xF2, 0xD0, 0x61, 0x30, 0x31, 0xDC, 0xB5, 0xD8, 0xD2, 0xEF, 0x1B, 0x32, 0x1F, 0xCE, 0xAD, 0x37, 0x7F, 0x62, 0x61, 0xE5, 0x47, 0xD8, 0x5D, 0x8E, 0xEC, 0x7F, 0x26, 0xE2, 0x32, 0x19, 0x07, 0x2F, 0x79, 0x55, 0xD0, 0xF8, 0xF6, 0x6D, 0xCD, 0x1E, 0x54, 0xC2, 0x01, 0xC7, 0x87 }, "42Rfc074wJUAkak7STa51Q2aDX5KuPeKmtzWdVWxbaVZZrmHkS3eKmkrBvlVZL3Oz")]
    [InlineData(new byte[] { 0xE8, 0x92, 0xD8, 0xF9, 0x4F, 0x61, 0x97, 0x6F, 0x1D, 0x1F, 0xA0, 0x1D, 0x19, 0xF4, 0x50, 0x1D, 0x29, 0x5F, 0x23, 0x22, 0x78, 0xCE, 0x3D, 0x7E, 0x14, 0x29, 0xD6, 0xA1, 0x85, 0x68, 0xA0, 0x7A, 0x87, 0xCA, 0x43, 0x99, 0xEA, 0xA1, 0x25, 0x04, 0xEA, 0x33, 0x25, 0x6D, 0x87, 0x43, 0xB2, 0x23, 0x7D, 0xBD, 0x91, 0x50, 0xE0, 0x9A, 0x04, 0x99, 0x35, 0x44, 0x87, 0x3B, 0x36, 0x4F, 0x8B, 0x90 }, "xyJuiwS7wXHWWmyg4RoWzA2FKFs4qbJgpE3nYe30ePTft7K98jLthmn2XrfnfXOJ66SBcgT1Q19lzf3t13pnza")]
    public void to_base62_matches_external_encoder_ground_truth(byte[] bytes, string expected)
    {
        bytes.ToBase62().Should().Be(expected);
    }
}
