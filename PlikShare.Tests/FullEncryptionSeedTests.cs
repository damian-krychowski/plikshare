using System.Security.Cryptography;
using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Tests;

/// <summary>
/// Coverage for the full-encryption seed primitives that replaced EncryptionSeed/EncryptionSeedWire:
///   - <see cref="FullEncryptionSeed"/> — the in-memory chain seed (Key + chain salts) that
///     derives metadata/file keys and decodes back via a <see cref="WorkspaceEncryptionSession"/>.
///   - <see cref="FullEncryptionSeedEphemeral"/> — the at-rest wire form whose key is wrapped by
///     an <see cref="EphemeralKeyRing"/> (memory-only, TTL-shredded) instead of a master key.
///   - <see cref="FullEncryptionSeedExtensions"/> — file metadata generation and file mode mapping.
/// </summary>
public class FullEncryptionSeedTests
{
    private const byte StorageDekVersion = 0;

    private static WorkspaceContext CreateEncryptedWorkspace() =>
        WorkspaceContextTestFactory.CreateEncrypted();

    private static WorkspaceContext CreatePlainWorkspace() =>
        WorkspaceContextTestFactory.CreatePlain();

    private static WorkspaceEncryptionSession CreateSession(
        int workspaceId = 42,
        byte[]? dekBytes = null)
    {
        return new WorkspaceEncryptionSession(
            workspaceId: workspaceId,
            entries:
            [
                new WorkspaceDekEntry
                {
                    StorageDekVersion = StorageDekVersion,
                    Dek = SecureBytes.CopyFrom(dekBytes ?? RandomBytes(32))
                }
            ]);
    }

    private static EphemeralKeyRing CreateRing(MutableClock clock) =>
        new(clock, new EphemeralKeyRingOptions());

    private static byte[] RandomBytes(int length) =>
        RandomNumberGenerator.GetBytes(length);

    private static byte[] DecodeEnvelope(EncodedMetadataValue encoded)
    {
        var raw = encoded.Encoded;
        Assert.StartsWith(AesGcmMetadataV1.ReservedPrefix, raw);
        return Convert.FromBase64String(raw[AesGcmMetadataV1.ReservedPrefix.Length..]);
    }

    // ---- FullEncryptionSeed.Prepare ----

    [Fact]
    public void Prepare_sets_key_version_from_latest_dek()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        Assert.Equal(StorageDekVersion, seed.IkmKeyVersion);
    }

    [Fact]
    public void Prepare_derives_32_byte_key()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        Assert.Equal(32, seed.Key.Length);
    }

    [Fact]
    public void Prepare_produces_single_metadata_chain_step()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        Assert.Single(seed.ChainStepSalts);
        Assert.Equal(32, seed.ChainStepSalts[0].Length);
    }

    [Fact]
    public void Prepare_ikm_chain_step_is_workspace_salt()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        Assert.Single(seed.IkmChainStepSalts);
        Assert.Equal(workspace.EncryptionMetadata!.Salt, seed.IkmChainStepSalts[0]);
    }

    [Fact]
    public void Prepare_throws_when_workspace_not_full_encrypted()
    {
        var workspace = CreatePlainWorkspace();
        using var session = CreateSession();

        Assert.Throws<InvalidOperationException>(
            () => FullEncryptionSeed.Prepare(workspace, session));
    }

    [Fact]
    public void Prepare_throws_when_workspace_id_does_not_match_session()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession(workspaceId: 999);

        Assert.Throws<InvalidOperationException>(
            () => FullEncryptionSeed.Prepare(workspace, session));
    }

    [Fact]
    public void Prepare_two_calls_use_different_chain_salts_and_keys()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var first = FullEncryptionSeed.Prepare(workspace, session);
        using var second = FullEncryptionSeed.Prepare(workspace, session);

        Assert.NotEqual(first.ChainStepSalts[0], second.ChainStepSalts[0]);
        Assert.NotEqual(first.Key, second.Key);
    }

    // ---- FullEncryptionSeed.DeriveNew ----

    [Fact]
    public void DeriveNew_appends_one_chain_step()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);
        using var derived = seed.DeriveNew();

        Assert.Equal(seed.ChainStepSalts.Count + 1, derived.ChainStepSalts.Count);
        Assert.Equal(seed.ChainStepSalts[0], derived.ChainStepSalts[0]);
        Assert.Equal(32, derived.ChainStepSalts[^1].Length);
    }

    [Fact]
    public void DeriveNew_preserves_ikm_version_and_ikm_salts()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);
        using var derived = seed.DeriveNew();

        Assert.Equal(seed.IkmKeyVersion, derived.IkmKeyVersion);
        Assert.Equal(seed.IkmChainStepSalts, derived.IkmChainStepSalts);
    }

    [Fact]
    public void DeriveNew_produces_different_key_than_parent()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);
        using var derived = seed.DeriveNew();

        Assert.NotEqual(seed.Key, derived.Key);
    }

    [Fact]
    public void DeriveNew_twice_uses_independent_salts()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);
        using var first = seed.DeriveNew();
        using var second = seed.DeriveNew();

        Assert.NotEqual(first.ChainStepSalts[^1], second.ChainStepSalts[^1]);
        Assert.NotEqual(first.Key, second.Key);
    }

    // ---- FullEncryptionSeed.Dispose ----

    [Fact]
    public void Dispose_zeroes_key_bytes()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        var seed = FullEncryptionSeed.Prepare(workspace, session);
        var keyBytes = seed.Key;

        Assert.Contains(keyBytes, b => b != 0);

        seed.Dispose();

        Assert.All(keyBytes, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        var seed = FullEncryptionSeed.Prepare(workspace, session);

        seed.Dispose();
        seed.Dispose();
    }

    // ---- FullEncryptionSeed: encode via seed, decode via session ----

    [Fact]
    public void EncodeMetadata_via_seed_then_decode_via_session_recovers_original()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        var encoded = seed.EncodeMetadata("secret-name.png");
        var decoded = session.DecodeEncryptableMetadata(encoded);

        Assert.Equal("secret-name.png", decoded);
    }

    [Fact]
    public void EncodeMetadata_via_seed_envelope_has_single_chain_step()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        var envelope = DecodeEnvelope(seed.EncodeMetadata("x"));

        // [format(1) | key_version(1) | chain_steps_count(1) | salts(N*32) | nonce(12) | ct | tag(16)]
        Assert.Equal(0x01, envelope[0]);
        Assert.Equal(StorageDekVersion, envelope[1]);
        Assert.Equal(1, envelope[2]);
    }

    [Fact]
    public void EncodeMetadata_via_seed_first_chain_step_equals_seed_salt()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        var envelope = DecodeEnvelope(seed.EncodeMetadata("anything"));

        Assert.Equal(seed.ChainStepSalts[0], envelope.AsSpan(3, 32).ToArray());
    }

    [Fact]
    public void EncodeMetadata_via_derived_seed_has_two_chain_steps_and_roundtrips()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);
        using var derived = seed.DeriveNew();

        var encoded = derived.EncodeMetadata("deep.png");

        var envelope = DecodeEnvelope(encoded);
        Assert.Equal(2, envelope[2]);

        Assert.Equal("deep.png", session.DecodeEncryptableMetadata(encoded));
    }

    [Fact]
    public void EncodeMetadata_via_seed_rejects_reserved_prefix()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        Assert.Throws<InvalidOperationException>(
            () => seed.EncodeMetadata("pse:smuggled"));
    }

    [Fact]
    public void EncodeMetadata_via_seed_with_wrong_dek_fails_to_decode()
    {
        var workspace = CreateEncryptedWorkspace();
        using var writer = CreateSession();
        using var attacker = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, writer);

        var encoded = seed.EncodeMetadata("secret");

        Assert.ThrowsAny<Exception>(
            () => attacker.DecodeEncryptableMetadata(encoded));
    }

    [Fact]
    public void Two_seeds_produce_envelopes_that_each_decode_via_same_session()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seedA = FullEncryptionSeed.Prepare(workspace, session);
        using var seedB = FullEncryptionSeed.Prepare(workspace, session);

        var encodedA = seedA.EncodeMetadata("from-seed-A");
        var encodedB = seedB.EncodeMetadata("from-seed-B");

        Assert.Equal("from-seed-A", session.DecodeEncryptableMetadata(encodedA));
        Assert.Equal("from-seed-B", session.DecodeEncryptableMetadata(encodedB));
    }

    // ---- FullEncryptionSeedEphemeral: prepare / decode roundtrip ----

    [Fact]
    public void Ephemeral_Prepare_encoded_key_is_ephemeral_prefixed()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var ephemeral = FullEncryptionSeedEphemeral.Prepare(workspace, session, ring);

        Assert.StartsWith(EphemeralKeyRing.ReservedPrefix, ephemeral.EncodedKey.Encoded);
    }

    [Fact]
    public void Ephemeral_Prepare_preserves_key_version()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var ephemeral = FullEncryptionSeedEphemeral.Prepare(workspace, session, ring);

        Assert.Equal(StorageDekVersion, ephemeral.IkmKeyVersion);
    }

    [Fact]
    public void Ephemeral_Prepare_throws_when_workspace_not_full_encrypted()
    {
        var workspace = CreatePlainWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        Assert.Throws<InvalidOperationException>(
            () => FullEncryptionSeedEphemeral.Prepare(workspace, session, ring));
    }

    [Fact]
    public void Ephemeral_Prepare_throws_when_workspace_id_does_not_match_session()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession(workspaceId: 999);
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        Assert.Throws<InvalidOperationException>(
            () => FullEncryptionSeedEphemeral.Prepare(workspace, session, ring));
    }

    [Fact]
    public void Ephemeral_TryDecode_returns_Ok_and_seed_decodes_metadata_via_session()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var ephemeral = FullEncryptionSeedEphemeral.Prepare(workspace, session, ring);

        var status = ephemeral.TryDecode(ring, out var seed);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.NotNull(seed);

        using (seed)
        {
            Assert.Equal(StorageDekVersion, seed!.IkmKeyVersion);

            var encoded = seed.EncodeMetadata("via-ephemeral.png");
            Assert.Equal("via-ephemeral.png", session.DecodeEncryptableMetadata(encoded));
        }
    }

    [Fact]
    public void Ephemeral_TryDecode_after_epoch_shredded_returns_Expired()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var ephemeral = FullEncryptionSeedEphemeral.Prepare(workspace, session, ring);

        clock.Advance(TimeSpan.FromHours(25) + TimeSpan.FromMinutes(1));
        ring.SweepExpired();

        var status = ephemeral.TryDecode(ring, out var seed);

        Assert.Equal(EphemeralDecodeStatus.Expired, status);
        Assert.Null(seed);
    }

    [Fact]
    public void Ephemeral_TryDecode_with_a_different_ring_returns_Expired()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var writerRing = CreateRing(clock);
        using var otherRing = CreateRing(clock);

        var ephemeral = FullEncryptionSeedEphemeral.Prepare(workspace, session, writerRing);

        var status = ephemeral.TryDecode(otherRing, out var seed);

        Assert.Equal(EphemeralDecodeStatus.Expired, status);
        Assert.Null(seed);
    }

    // ---- FullEncryptionSeedEphemeral.FromFile ----

    [Fact]
    public void Ephemeral_FromFile_rebuilds_seed_that_produces_same_file_key()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        using var original = FullEncryptionSeed.Prepare(workspace, session);
        var expectedFileKey = (byte[])original.Key.Clone();

        var metadata = original.GenerateFileEncryptionMetadata();

        var ephemeral = FullEncryptionSeedEphemeral.FromFile(metadata, workspace, session, ring);

        var status = ephemeral.TryDecode(ring, out var rebuilt);
        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.NotNull(rebuilt);

        using (rebuilt)
        {
            var mode = rebuilt!.ToFileEncryptionMode(metadata);
            var v2 = Assert.IsType<AesGcmV2Encryption>(mode);

            Assert.Equal(expectedFileKey, v2.Input.FileKey);
        }
    }

    [Fact]
    public void Ephemeral_FromFile_throws_when_first_chain_salt_is_not_workspace_salt()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        using var seed = FullEncryptionSeed.Prepare(workspace, session);
        var metadata = seed.GenerateFileEncryptionMetadata();

        var tamperedChain = metadata.ChainStepSalts.Select(s => (byte[])s.Clone()).ToArray();
        tamperedChain[0][0] ^= 0xFF;

        var tampered = new FileEncryptionMetadata
        {
            FormatVersion = metadata.FormatVersion,
            KeyVersion = metadata.KeyVersion,
            Salt = metadata.Salt,
            NoncePrefix = metadata.NoncePrefix,
            ChainStepSalts = tamperedChain
        };

        Assert.Throws<InvalidOperationException>(
            () => FullEncryptionSeedEphemeral.FromFile(tampered, workspace, session, ring));
    }

    [Fact]
    public void Ephemeral_FromFile_throws_on_empty_chain_steps()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var metadata = new FileEncryptionMetadata
        {
            FormatVersion = 2,
            KeyVersion = StorageDekVersion,
            Salt = RandomBytes(32),
            NoncePrefix = Aes256GcmStreamingV2.GenerateNoncePrefix(),
            ChainStepSalts = []
        };

        Assert.Throws<InvalidOperationException>(
            () => FullEncryptionSeedEphemeral.FromFile(metadata, workspace, session, ring));
    }

    // ---- FullEncryptionSeedExtensions: file metadata + mode ----

    [Fact]
    public void GenerateFileEncryptionMetadata_uses_format_version_2_and_seed_key_version()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        var metadata = seed.GenerateFileEncryptionMetadata();

        Assert.Equal(2, metadata.FormatVersion);
        Assert.Equal(seed.IkmKeyVersion, metadata.KeyVersion);
    }

    [Fact]
    public void GenerateFileEncryptionMetadata_salt_is_last_seed_chain_salt()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);

        var metadata = seed.GenerateFileEncryptionMetadata();

        Assert.Equal(seed.ChainStepSalts[^1], metadata.Salt);
    }

    [Fact]
    public void ToFileEncryptionMode_produces_v2_mode_with_seed_key()
    {
        var workspace = CreateEncryptedWorkspace();
        using var session = CreateSession();

        using var seed = FullEncryptionSeed.Prepare(workspace, session);
        var expectedFileKey = (byte[])seed.Key.Clone();

        var metadata = seed.GenerateFileEncryptionMetadata();
        var mode = seed.ToFileEncryptionMode(metadata);

        var v2 = Assert.IsType<AesGcmV2Encryption>(mode);
        Assert.Equal(expectedFileKey, v2.Input.FileKey);
    }

    private sealed class MutableClock : IClock
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public DateTimeOffset UtcNow => _utcNow;
        public DateTime Now => _utcNow.UtcDateTime;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
