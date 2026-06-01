using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

/// <summary>
/// Black-box round-trip coverage for the metadata encrypt/decrypt path. Each test seeds a
/// <see cref="WorkspaceEncryptionSession"/> with a known 32-byte DEK, encodes a plaintext
/// through <see cref="EncryptableMetadataExtensions.Encode"/> (or via the
/// <see cref="EncryptableMetadata"/> abstraction), then decodes through
/// <see cref="EncryptableMetadataExtensions.DecodeEncryptableMetadata"/> and asserts the
/// recovered string is byte-for-byte identical to the original.
///
/// Tests cover: short strings, empty string, UTF-8 multi-byte, stack/heap paths
/// (StackAllocThresholdBytes is 512), every-encode-is-different (random salt + nonce),
/// passthrough when the session is null, ReservedPrefix rejection, envelope chain layout,
/// and the sentinel that catches reuse of an already-disposed <see cref="MetadataAesInputsV1"/>.
/// </summary>
public class EncryptableMetadataExtensionsTests
{
    private const byte StorageDekVersion = 0;

    private static WorkspaceEncryptionSession CreateSession(byte[]? dekBytes = null)
    {
        var bytes = dekBytes ?? RandomBytes(32);

        return new WorkspaceEncryptionSession(
            workspaceId: 42,
            entries:
            [
                new WorkspaceDekEntry
                {
                    StorageDekVersion = StorageDekVersion,
                    Dek = SecureBytes.CopyFrom(bytes)
                }
            ]);
    }

    private static byte[] RandomBytes(int length)
    {
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    private static byte[] DecodeEnvelope(string encoded)
    {
        Assert.StartsWith(EncryptableMetadataExtensions.ReservedPrefix, encoded);

        var base64 = encoded[EncryptableMetadataExtensions.ReservedPrefix.Length..];

        return Convert.FromBase64String(base64);
    }

    // ---- Round-trip happy paths ----

    [Fact]
    public void Encode_then_Decode_short_ascii_recovers_original()
    {
        using var session = CreateSession();

        var encoded = session.Encode("photo.jpg");

        var decoded = session.DecodeEncryptableMetadata(encoded);

        Assert.Equal("photo.jpg", decoded);
    }

    [Fact]
    public void Encode_then_Decode_empty_string_recovers_original()
    {
        using var session = CreateSession();

        var encoded = session.Encode("");

        var decoded = session.DecodeEncryptableMetadata(encoded);

        Assert.Equal("", decoded);
    }

    [Fact]
    public void Encode_then_Decode_single_character_recovers_original()
    {
        using var session = CreateSession();

        var encoded = session.Encode("x");

        var decoded = session.DecodeEncryptableMetadata(encoded);

        Assert.Equal("x", decoded);
    }

    [Fact]
    public void Encode_then_Decode_medium_string_below_stackalloc_threshold_recovers_original()
    {
        using var session = CreateSession();

        // 400 bytes UTF-8 — well below the 512-byte stackalloc threshold inside Encode.
        var original = new string('a', 400);

        var encoded = session.Encode(original);

        Assert.Equal(original, session.DecodeEncryptableMetadata(encoded));
    }

    [Fact]
    public void Encode_then_Decode_large_string_above_stackalloc_threshold_recovers_original()
    {
        using var session = CreateSession();

        // 4096 bytes — forces the ArrayPool branch for both envelope and plaintext buffers
        // on the encode side. Catches any slice-length / offset mismatch on the heap path.
        var original = new string('Z', 4096);

        var encoded = session.Encode(original);

        Assert.Equal(original, session.DecodeEncryptableMetadata(encoded));
    }

    [Fact]
    public void Encode_then_Decode_huge_string_recovers_original()
    {
        using var session = CreateSession();

        // 1 MiB — note/comment-sized payload.
        var original = new string('!', 1 * 1024 * 1024);

        var encoded = session.Encode(original);

        Assert.Equal(original, session.DecodeEncryptableMetadata(encoded));
    }

    // ---- Format invariants ----

    [Fact]
    public void Encoded_envelope_starts_with_reserved_prefix()
    {
        using var session = CreateSession();

        var encoded = session.Encode("anything").Encoded;

        Assert.StartsWith(EncryptableMetadataExtensions.ReservedPrefix, encoded);
    }

    [Fact]
    public void Encoded_envelope_has_chain_steps_count_of_one()
    {
        using var session = CreateSession();

        var envelope = DecodeEnvelope(session.Encode("hello").Encoded);

        // [format(1) | key_version(1) | chain_steps_count(1) | salts(N*32) | nonce(12) | ct | tag(16)]
        Assert.Equal(0x01, envelope[0]);
        Assert.Equal(StorageDekVersion, envelope[1]);
        Assert.Equal(1, envelope[2]);
    }

    [Fact]
    public void Encoded_envelope_carries_32_byte_chain_salt()
    {
        using var session = CreateSession();

        var envelope = DecodeEnvelope(session.Encode("hello").Encoded);

        // header (3) + chain salt (32) + nonce (12) + ciphertext (5 = "hello") + tag (16) = 68
        Assert.Equal(3 + 32 + 12 + 5 + 16, envelope.Length);
    }

    [Fact]
    public void Two_encodes_of_same_value_produce_different_envelopes()
    {
        // Each encode generates a fresh random chain salt + fresh random nonce, so the
        // ciphertext (and therefore the entire envelope) must differ even for the same
        // plaintext. If this ever passes by accident the RNG is broken.
        using var session = CreateSession();

        var first = session.Encode("same-value").Encoded;
        var second = session.Encode("same-value").Encoded;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Two_encodes_of_same_value_use_different_chain_salts()
    {
        using var session = CreateSession();

        var first = DecodeEnvelope(session.Encode("same").Encoded);
        var second = DecodeEnvelope(session.Encode("same").Encoded);

        // Bytes 3..35 are the chain salt.
        Assert.NotEqual(
            first.AsSpan(3, 32).ToArray(),
            second.AsSpan(3, 32).ToArray());
    }

    [Fact]
    public void Two_encodes_of_same_value_use_different_nonces()
    {
        using var session = CreateSession();

        var first = DecodeEnvelope(session.Encode("same").Encoded);
        var second = DecodeEnvelope(session.Encode("same").Encoded);

        // Nonce sits right after the 32-byte chain salt: bytes 35..47.
        Assert.NotEqual(
            first.AsSpan(35, 12).ToArray(),
            second.AsSpan(35, 12).ToArray());
    }

    // ---- Null-session passthrough ----

    [Fact]
    public void Encode_with_null_session_returns_plaintext_unchanged()
    {
        WorkspaceEncryptionSession? session = null;

        var encoded = session.Encode("plain").Encoded;

        Assert.Equal("plain", encoded);
    }

    [Fact]
    public void Decode_with_null_session_returns_input_unchanged()
    {
        WorkspaceEncryptionSession? session = null;

        var decoded = session.DecodeEncryptableMetadata("plain");

        Assert.Equal("plain", decoded);
    }

    // ---- ReservedPrefix rejection ----

    [Fact]
    public void Encode_throws_when_value_starts_with_reserved_prefix()
    {
        using var session = CreateSession();

        Assert.Throws<InvalidOperationException>(
            () => session.Encode("pse:smuggled"));
    }

    [Fact]
    public void ToEncryptableMetadata_throws_when_value_starts_with_reserved_prefix()
    {
        using var session = CreateSession();

        Assert.Throws<InvalidOperationException>(
            () => session.ToEncryptableMetadata("pse:smuggled"));
    }

    // ---- EncryptableMetadata abstraction roundtrip ----

    [Fact]
    public void ToEncryptableMetadata_then_Encode_then_Decode_recovers_original()
    {
        using var session = CreateSession();

        var metadata = session.ToEncryptableMetadata("file.png");

        var encoded = metadata.Encode();
        var decoded = session.DecodeEncryptableMetadata(encoded);

        Assert.Equal("file.png", decoded);
    }

    [Fact]
    public void ToEncryptableMetadata_with_null_session_returns_no_encryption_mode()
    {
        WorkspaceEncryptionSession? session = null;

        var metadata = session.ToEncryptableMetadata("plain");

        Assert.IsType<NoMetadataEncryption>(metadata.EncryptionMode);
        Assert.Equal("plain", metadata.Encode().Encoded);
    }

    // ---- Sentinel: reuse-detection ----

    [Fact]
    public void Encode_on_same_EncryptableMetadata_twice_throws_on_second_call()
    {
        // This is the bug we are hunting: EncodeAesGcmV1 does `using var input = aesInput`,
        // which zeroes MetadataAesInputsV1.MetadataKey at end of scope. If callers re-encode
        // the same EncryptableMetadata, the second call sees an all-zero key. The sentinel
        // at the top of EncodeAesGcmV1 throws ObjectDisposedException so the bug is caught
        // here instead of silently producing a broken ciphertext.
        using var session = CreateSession();

        var metadata = session.ToEncryptableMetadata("repeat");

        _ = metadata.Encode();

        Assert.Throws<ObjectDisposedException>(
            () => metadata.Encode());
    }

    // ---- Cross-session decoding (same DEK material) ----

    [Fact]
    public void Encrypted_by_one_session_decodes_with_another_session_holding_same_dek()
    {
        // Two distinct WorkspaceEncryptionSession instances seeded with byte-identical DEK
        // material — simulating the same workspace unlocked by two requests. The chain salt
        // is embedded in the envelope so decode has everything it needs from the session DEK.
        var dekBytes = RandomBytes(32);

        using var writer = CreateSession(dekBytes);
        using var reader = CreateSession(dekBytes);

        var encoded = writer.Encode("shared.txt");
        var decoded = reader.DecodeEncryptableMetadata(encoded);

        Assert.Equal("shared.txt", decoded);
    }

    // ---- Wrong DEK / corrupted envelope ----

    [Fact]
    public void Decode_with_wrong_dek_throws_on_tag_mismatch()
    {
        // Encrypt with one DEK, try to decrypt with a different DEK. The auth tag check
        // inside AES-GCM fails — this is exactly the symptom the user is debugging in
        // the real flow, and it must surface as an exception rather than a silent corruption.
        using var writer = CreateSession();
        using var attacker = CreateSession();

        var encoded = writer.Encode("secret");

        Assert.ThrowsAny<Exception>(
            () => attacker.DecodeEncryptableMetadata(encoded));
    }

    [Fact]
    public void Decode_throws_when_envelope_does_not_start_with_reserved_prefix()
    {
        using var session = CreateSession();

        Assert.Throws<InvalidOperationException>(
            () => session.DecodeEncryptableMetadata("no-prefix-here"));
    }

    [Fact]
    public void Decode_throws_on_malformed_base64()
    {
        using var session = CreateSession();

        Assert.ThrowsAny<Exception>(
            () => session.DecodeEncryptableMetadata("pse:!!!not-base64!!!"));
    }

    [Fact]
    public void Decode_throws_on_unsupported_format_byte()
    {
        using var session = CreateSession();

        // Build a syntactically valid envelope but flip the format byte to 0xFF.
        var envelope = DecodeEnvelope(session.Encode("x").Encoded);
        envelope[0] = 0xFF;

        var tampered = EncryptableMetadataExtensions.ReservedPrefix + Convert.ToBase64String(envelope);

        Assert.Throws<InvalidOperationException>(
            () => session.DecodeEncryptableMetadata(tampered));
    }

    [Fact]
    public void Decode_throws_on_unknown_key_version()
    {
        using var session = CreateSession();

        var envelope = DecodeEnvelope(session.Encode("x").Encoded);
        // Key version 0 is what the session has; bump to 99 so the lookup throws.
        envelope[1] = 99;

        var tampered = EncryptableMetadataExtensions.ReservedPrefix + Convert.ToBase64String(envelope);

        Assert.ThrowsAny<Exception>(
            () => session.DecodeEncryptableMetadata(tampered));
    }

    [Fact]
    public void Decode_throws_on_tampered_ciphertext()
    {
        // Flip one bit of the ciphertext — AES-GCM's authenticator must reject the envelope.
        using var session = CreateSession();

        var envelope = DecodeEnvelope(session.Encode("hello").Encoded);

        // header(3) + chainSalt(32) + nonce(12) = 47. First ciphertext byte at offset 47.
        envelope[47] ^= 0x01;

        var tampered = EncryptableMetadataExtensions.ReservedPrefix + Convert.ToBase64String(envelope);

        Assert.ThrowsAny<Exception>(
            () => session.DecodeEncryptableMetadata(tampered));
    }

    [Fact]
    public void Decode_throws_on_tampered_chain_salt()
    {
        // Modifying the salt makes the decoder derive a different metadata DEK than encode
        // used — AES-GCM tag verification fails. Confirms the salt is actually being read
        // from the envelope and fed into key derivation, not ignored.
        using var session = CreateSession();

        var envelope = DecodeEnvelope(session.Encode("hello").Encoded);

        // First chain salt byte at offset 3.
        envelope[3] ^= 0x01;

        var tampered = EncryptableMetadataExtensions.ReservedPrefix + Convert.ToBase64String(envelope);

        Assert.ThrowsAny<Exception>(
            () => session.DecodeEncryptableMetadata(tampered));
    }

    // ---- Stability across many sequential encodes ----

    [Fact]
    public void Many_sequential_encode_decode_roundtrips_all_recover_input()
    {
        // 200 distinct inputs encoded and decoded back-to-back. If state leaks between calls
        // (e.g. a shared buffer that holds stale ciphertext, or an RNG that repeats), some
        // of these will mis-decode. With a clean per-call setup all 200 must round-trip.
        using var session = CreateSession();

        for (var i = 0; i < 200; i++)
        {
            var original = $"item-{i}-{Guid.NewGuid()}";

            var encoded = session.Encode(original);
            var decoded = session.DecodeEncryptableMetadata(encoded);

            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void Many_distinct_values_through_EncryptableMetadata_abstraction_all_roundtrip()
    {
        // Same as above but routes every value through ToEncryptableMetadata -> Encode (the
        // path SQLite binders actually use), so the abstraction itself is exercised end-to-end.
        using var session = CreateSession();

        for (var i = 0; i < 50; i++)
        {
            var original = $"abs-{i}";

            var metadata = session.ToEncryptableMetadata(original);
            var encoded = metadata.Encode();
            var decoded = session.DecodeEncryptableMetadata(encoded);

            Assert.Equal(original, decoded);
        }
    }

    // ---- Ciphertext length matches plaintext UTF-8 byte length ----

    [Fact]
    public void Encoded_envelope_size_grows_linearly_with_utf8_length()
    {
        using var session = CreateSession();

        var shortEnv = DecodeEnvelope(session.Encode("a").Encoded);          // 1 utf8 byte
        var longerEnv = DecodeEnvelope(session.Encode("aaaaaa").Encoded);    // 6 utf8 bytes

        Assert.Equal(longerEnv.Length - shortEnv.Length, 5);
    }

    [Fact]
    public void Encoded_envelope_utf8_byte_count_matches_unicode_input()
    {
        using var session = CreateSession();

        // "ą" is 2 bytes UTF-8 — envelope ciphertext region must reflect that, not the
        // string's char count.
        var asciiEnv = DecodeEnvelope(session.Encode("a").Encoded);
        var polishEnv = DecodeEnvelope(session.Encode("ą").Encoded);

        Assert.Equal(polishEnv.Length - asciiEnv.Length, 1);

        // Sanity: ąść is 6 UTF-8 bytes, "abc" is 3 — diff = 3.
        var threeAsciiEnv = DecodeEnvelope(session.Encode("abc").Encoded);
        var threePolishEnv = DecodeEnvelope(session.Encode("ąść").Encoded);
        Assert.Equal(threePolishEnv.Length - threeAsciiEnv.Length, 3);

        Assert.Equal(6, Encoding.UTF8.GetByteCount("ąść"));
    }
}
