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

    /// <summary>
    /// Shared master-encryption instance for wire-form tests. PBKDF2 stretch costs ~500ms
    /// per password at startup; lazy class-static keeps the cost paid exactly once for the
    /// whole test class instead of per [Fact].
    /// </summary>
    private static readonly Lazy<IMasterDataEncryption> SharedMasterEncryption = new(
        () => new AesGcmMasterDataEncryption(
            new MasterEncryptionKeyProvider(["test-master-password"])));

    private static IMasterDataEncryption MasterEncryption => SharedMasterEncryption.Value;

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
        Assert.StartsWith(AesGcmMetadataV1.ReservedPrefix, encoded);

        var base64 = encoded[AesGcmMetadataV1.ReservedPrefix.Length..];

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

        Assert.StartsWith(AesGcmMetadataV1.ReservedPrefix, encoded);
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

        var tampered = AesGcmMetadataV1.ReservedPrefix + Convert.ToBase64String(envelope);

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

        var tampered = AesGcmMetadataV1.ReservedPrefix + Convert.ToBase64String(envelope);

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

        var tampered = AesGcmMetadataV1.ReservedPrefix + Convert.ToBase64String(envelope);

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

        var tampered = AesGcmMetadataV1.ReservedPrefix + Convert.ToBase64String(envelope);

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

    // ---- EncryptionSeed: encrypt via pre-derived seed, decrypt via session ----

    [Fact]
    public void EncryptionSeed_DeriveNew_produces_random_salt()
    {
        // Two derivations from the same workspace DEK must produce different salts and seeds.
        // Without that, "fresh seed per trigger" guarantee is broken.
        using var session = CreateSession();

        using var first = EncryptionSeed.DeriveNew(session);
        using var second = EncryptionSeed.DeriveNew(session);

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Seed, second.Seed);
    }

    [Fact]
    public void EncryptionSeed_DeriveNew_preserves_workspace_dek_version()
    {
        using var session = CreateSession();

        using var seed = EncryptionSeed.DeriveNew(session);

        Assert.Equal(StorageDekVersion, seed.KeyVersion);
    }

    [Fact]
    public void EncryptionSeed_salt_and_seed_are_32_bytes()
    {
        using var session = CreateSession();

        using var seed = EncryptionSeed.DeriveNew(session);

        Assert.Equal(32, seed.Salt.Length);
        Assert.Equal(32, seed.Seed.Length);
    }

    [Fact]
    public void EncryptionSeed_Dispose_zeroes_seed_bytes()
    {
        using var session = CreateSession();
        var seed = EncryptionSeed.DeriveNew(session);

        // Capture reference before dispose so we can inspect the buffer afterwards.
        var seedBytes = seed.Seed;

        // Sanity: HKDF output is overwhelmingly unlikely to be all-zeros pre-dispose.
        Assert.Contains(seedBytes, b => b != 0);

        seed.Dispose();

        Assert.All(seedBytes, b => Assert.Equal(0, b));
    }

    [Fact]
    public void EncryptionSeed_Dispose_is_idempotent()
    {
        using var session = CreateSession();
        var seed = EncryptionSeed.DeriveNew(session);

        seed.Dispose();
        seed.Dispose();
        // Implicit assertion: second dispose did not throw.
    }

    [Fact]
    public void Encode_via_seed_then_Decode_via_session_recovers_original()
    {
        // The whole point of the seed primitive: a holder of the seed (no workspace DEK)
        // can produce metadata keys, and a holder of the workspace DEK (via session) can
        // decode them by walking the embedded chain salts.
        using var session = CreateSession();
        using var seed = EncryptionSeed.DeriveNew(session);

        var input = MetadataAesInputsV1.Prepare(seed);

        var metadata = new EncryptableMetadata(
            Value: "secret-name.png",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: input));

        var encoded = metadata.Encode();
        var decoded = session.DecodeEncryptableMetadata(encoded);

        Assert.Equal("secret-name.png", decoded);
    }

    [Fact]
    public void Encode_via_seed_envelope_has_two_chain_steps()
    {
        using var session = CreateSession();
        using var seed = EncryptionSeed.DeriveNew(session);

        var input = MetadataAesInputsV1.Prepare(seed);

        var metadata = new EncryptableMetadata(
            Value: "x",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: input));

        var envelope = DecodeEnvelope(metadata.Encode().Encoded);

        // [format(1) | key_version(1) | chain_steps_count(1) | salts(2*32) | nonce(12) | ct | tag(16)]
        Assert.Equal(0x01, envelope[0]);
        Assert.Equal(StorageDekVersion, envelope[1]);
        Assert.Equal(2, envelope[2]);
        Assert.Equal(3 + 64 + 12 + 1 + 16, envelope.Length);
    }

    [Fact]
    public void Encode_via_seed_envelope_first_chain_step_equals_seed_salt()
    {
        // The decoder reconstructs metadata_key by walking [seed.Salt, metadataSalt] from
        // the workspace DEK. The first 32 bytes of chain salts in the envelope therefore
        // MUST equal seed.Salt — anything else and the derivation diverges and the tag fails.
        using var session = CreateSession();
        using var seed = EncryptionSeed.DeriveNew(session);

        var input = MetadataAesInputsV1.Prepare(seed);

        var metadata = new EncryptableMetadata(
            Value: "anything",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: input));

        var envelope = DecodeEnvelope(metadata.Encode().Encoded);

        // Bytes 3..35 are the first chain step salt.
        var firstStepInEnvelope = envelope.AsSpan(3, 32).ToArray();

        Assert.Equal(seed.Salt, firstStepInEnvelope);
    }

    [Fact]
    public void Encode_via_seed_envelope_second_chain_step_is_random_per_value()
    {
        // Each encode through the seed must use a FRESH metadataSalt — otherwise two values
        // sharing the same seed would share AES keys and nonces independently, weakening
        // GCM security guarantees per-value.
        using var session = CreateSession();
        using var seed = EncryptionSeed.DeriveNew(session);

        var firstInput = MetadataAesInputsV1.Prepare(seed);
        var firstEnv = DecodeEnvelope(new EncryptableMetadata(
            Value: "v1",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: firstInput))
            .Encode().Encoded);

        var secondInput = MetadataAesInputsV1.Prepare(seed);
        var secondEnv = DecodeEnvelope(new EncryptableMetadata(
            Value: "v2",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: secondInput))
            .Encode().Encoded);

        // Bytes 35..67 are the second chain step salt.
        var firstMetadataSalt = firstEnv.AsSpan(35, 32).ToArray();
        var secondMetadataSalt = secondEnv.AsSpan(35, 32).ToArray();

        Assert.NotEqual(firstMetadataSalt, secondMetadataSalt);

        // ... but the first chain step (seed salt) is shared, since the seed is shared.
        Assert.Equal(
            firstEnv.AsSpan(3, 32).ToArray(),
            secondEnv.AsSpan(3, 32).ToArray());
    }

    [Fact]
    public void Encode_via_seed_multiple_values_all_roundtrip_with_same_seed()
    {
        // The actual workflow: trigger time derives ONE seed, hands it to a worker that
        // encrypts N pieces of metadata. Each Prepare(seed) call produces a fresh per-value
        // MetadataAesInputsV1 — the seed itself is reused. The session then decodes them all.
        using var session = CreateSession();
        using var seed = EncryptionSeed.DeriveNew(session);

        var inputs = new[]
        {
            "content-type",
            "filename.webp",
            ".webp",
            "{\"variant\":\"Mini\",\"etag\":\"abc\"}"
        };

        var encoded = new List<EncodedMetadataValue>();
        foreach (var value in inputs)
        {
            var aes = MetadataAesInputsV1.Prepare(seed);
            var metadata = new EncryptableMetadata(
                Value: value,
                EncryptionMode: new AesGcmMetadataV1Encryption(Input: aes));

            encoded.Add(metadata.Encode());
        }

        for (var i = 0; i < inputs.Length; i++)
        {
            Assert.Equal(inputs[i], session.DecodeEncryptableMetadata(encoded[i]));
        }
    }

    [Fact]
    public void Encode_via_seed_can_be_decoded_by_a_different_session_with_same_dek()
    {
        // Encrypt with a worker that built a seed from workspace DEK X, decrypt with a
        // separate session also seeded with byte-identical DEK X — the two sessions are
        // independent objects but share material, so decode works end-to-end.
        var dekBytes = RandomBytes(32);

        using var writerSession = CreateSession(dekBytes);
        using var readerSession = CreateSession(dekBytes);

        using var seed = EncryptionSeed.DeriveNew(writerSession);

        var input = MetadataAesInputsV1.Prepare(seed);
        var metadata = new EncryptableMetadata(
            Value: "cross-session.png",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: input));

        var encoded = metadata.Encode();
        var decoded = readerSession.DecodeEncryptableMetadata(encoded);

        Assert.Equal("cross-session.png", decoded);
    }

    [Fact]
    public void Encode_via_seed_then_Decode_via_session_with_different_dek_throws_tag_mismatch()
    {
        // A reader holding a different workspace DEK walks the chain from a different
        // starting point and arrives at a different metadata_key — AES-GCM tag check fails.
        // Confirms the security boundary: a leaked seed alone is useless against an envelope
        // whose decoder reconstructs the chain from a different workspace DEK.
        using var writerSession = CreateSession();
        using var attackerSession = CreateSession();

        using var seed = EncryptionSeed.DeriveNew(writerSession);

        var input = MetadataAesInputsV1.Prepare(seed);
        var metadata = new EncryptableMetadata(
            Value: "secret",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: input));

        var encoded = metadata.Encode();

        Assert.ThrowsAny<Exception>(
            () => attackerSession.DecodeEncryptableMetadata(encoded));
    }

    [Fact]
    public void Two_different_seeds_produce_envelopes_that_each_decode_independently()
    {
        // No cross-contamination: seed A's envelope must NOT accidentally decode under
        // seed B's chain (which it can't, since the envelope carries its own salts) — but
        // both must decode under the same workspace session because the chain anchors at
        // the workspace DEK.
        using var session = CreateSession();
        using var seedA = EncryptionSeed.DeriveNew(session);
        using var seedB = EncryptionSeed.DeriveNew(session);

        var inputA = MetadataAesInputsV1.Prepare(seedA);
        var encodedA = new EncryptableMetadata(
            Value: "from-seed-A",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: inputA))
            .Encode();

        var inputB = MetadataAesInputsV1.Prepare(seedB);
        var encodedB = new EncryptableMetadata(
            Value: "from-seed-B",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: inputB))
            .Encode();

        Assert.Equal("from-seed-A", session.DecodeEncryptableMetadata(encodedA));
        Assert.Equal("from-seed-B", session.DecodeEncryptableMetadata(encodedB));
    }

    // ---- EncryptionSeedWire: master-encrypted wire form ----

    [Fact]
    public void EncryptionSeedWire_Prepare_does_not_leak_plaintext_seed_into_the_wire()
    {
        // EncryptedSeed must NOT be the raw seed bytes — it's the ciphertext frame. We
        // can't directly compare to the plaintext (we never see it on the heap), but the
        // wire payload has a fixed envelope around the seed (format byte, master key id,
        // nonce, ciphertext, tag), so it MUST be larger than the bare 32 bytes.
        using var session = CreateSession();

        var wire = EncryptionSeedWire.Prepare(session, MasterEncryption);

        Assert.Equal(32, wire.Salt.Length);
        Assert.True(wire.EncryptedSeed.Length > 32);
    }

    [Fact]
    public void EncryptionSeedWire_Prepare_preserves_workspace_dek_version()
    {
        using var session = CreateSession();

        var wire = EncryptionSeedWire.Prepare(session, MasterEncryption);

        Assert.Equal(StorageDekVersion, wire.KeyVersion);
    }

    [Fact]
    public void EncryptionSeedWire_two_Prepare_calls_produce_different_wires()
    {
        // Different seed salts AND different master-encryption nonces — every byte of
        // EncryptedSeed must differ. If they ever match the RNG is broken.
        using var session = CreateSession();

        var first = EncryptionSeedWire.Prepare(session, MasterEncryption);
        var second = EncryptionSeedWire.Prepare(session, MasterEncryption);

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.EncryptedSeed, second.EncryptedSeed);
    }

    [Fact]
    public void EncryptionSeed_Prepare_to_wire_then_FromWire_recovers_same_seed_bytes()
    {
        // The whole wire roundtrip: take a workspace DEK, prepare a wire (master-encrypted),
        // unwrap via FromWire, and confirm the recovered seed bytes match what an in-memory
        // EncryptionSeed produced under the same salt would have given us.
        using var session = CreateSession();

        var wire = EncryptionSeedWire.Prepare(session, MasterEncryption);

        using var unwrapped = wire.Unwrap(MasterEncryption);

        Assert.Equal(wire.KeyVersion, unwrapped.KeyVersion);
        Assert.Equal(wire.Salt, unwrapped.Salt);

        // Reproduce the same derivation independently and compare seed bytes.
        var latest = session.GetLatestDek();
        var expectedSeed = new byte[32];
        latest.Dek.DeriveKey(
            chainStepSalts: wire.Salt,
            output: expectedSeed);

        Assert.Equal(expectedSeed, unwrapped.Seed);
    }

    [Fact]
    public void EncryptionSeed_FromWire_with_wrong_master_encryption_throws()
    {
        // A reader holding a different master password — its stretched key differs, AES-GCM
        // tag check on the wire fails. Confirms the wire's security boundary: ciphertext is
        // useless without the original master key.
        using var session = CreateSession();

        var wire = EncryptionSeedWire.Prepare(session, MasterEncryption);

        var attackerMaster = new AesGcmMasterDataEncryption(
            new MasterEncryptionKeyProvider(["different-master-password"]));

        Assert.ThrowsAny<Exception>(
            () => wire.Unwrap(attackerMaster));
    }

    [Fact]
    public void Full_pipeline_wire_to_seed_to_metadata_input_to_decode_via_session()
    {
        // End-to-end: trigger time builds the WIRE (master-encrypted), passes it through
        // a queue payload (simulated by holding the wire object), worker unwraps via
        // FromWire, builds metadata inputs, encrypts a value. Application later reads it
        // back via the workspace session. Exercises every link in the chain.
        using var session = CreateSession();

        // Trigger-time: derive wire (master-encrypted seed).
        var wire = EncryptionSeedWire.Prepare(session, MasterEncryption);

        // "Worker-time": unwrap, encrypt — NO workspace session here, only the wire
        // and the master encryption (which the worker has via DI as a process singleton).
        using var workerSeed = wire.Unwrap(MasterEncryption);

        var input = MetadataAesInputsV1.Prepare(workerSeed);
        var metadata = new EncryptableMetadata(
            Value: "shared-via-wire.png",
            EncryptionMode: new AesGcmMetadataV1Encryption(Input: input));

        var encoded = metadata.Encode();

        // Application-time: decode via session (workspace DEK), no seed/wire involved.
        var decoded = session.DecodeEncryptableMetadata(encoded);

        Assert.Equal("shared-via-wire.png", decoded);
    }

    [Fact]
    public void Full_pipeline_wire_seed_can_encrypt_multiple_metadatas_all_decoded_by_session()
    {
        // Matches the real worker workflow: ONE wire unwrapped to ONE seed, the seed reused
        // for N per-value metadata encryptions, all decoded later by the session.
        using var session = CreateSession();

        var wire = EncryptionSeedWire.Prepare(session, MasterEncryption);

        var inputs = new[]
        {
            "image/webp",
            "thumb-mini.webp",
            ".webp"
        };

        var encoded = new List<EncodedMetadataValue>();
        using (var workerSeed = wire.Unwrap(MasterEncryption))
        {
            foreach (var value in inputs)
            {
                var aes = MetadataAesInputsV1.Prepare(workerSeed);
                var metadata = new EncryptableMetadata(
                    Value: value,
                    EncryptionMode: new AesGcmMetadataV1Encryption(Input: aes));

                encoded.Add(metadata.Encode());
            }
        }

        for (var i = 0; i < inputs.Length; i++)
        {
            Assert.Equal(inputs[i], session.DecodeEncryptableMetadata(encoded[i]));
        }
    }
}
