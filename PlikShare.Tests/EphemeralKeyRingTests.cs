using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;

namespace PlikShare.Tests;

public class EphemeralKeyRingTests
{
    private static EphemeralKeyRing CreateRing(
        MutableClock clock,
        EphemeralKeyRingOptions? options = null)
    {
        return new EphemeralKeyRing(
            clock,
            options ?? new EphemeralKeyRingOptions());
    }

    private static ulong EpochIdOf(EncodedEphemeralValue value)
    {
        var frame = FrameOf(value);
        return BinaryPrimitives.ReadUInt64BigEndian(frame.AsSpan(1, 8));
    }

    private static byte[] FrameOf(EncodedEphemeralValue value)
    {
        var base64 = value.Encoded[EphemeralKeyRing.ReservedPrefix.Length..];
        return Convert.FromBase64String(base64);
    }

    private static EncodedEphemeralValue Wrap(byte[] frame)
    {
        return new EncodedEphemeralValue(
            EphemeralKeyRing.ReservedPrefix + Convert.ToBase64String(frame));
    }

    // ---- Round-trip happy paths ----

    [Fact]
    public void Encode_then_TryDecode_recovers_ascii()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var encoded = ring.Encode("photo.jpg");

        var status = ring.TryDecode(encoded, out string decoded);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal("photo.jpg", decoded);
    }

    [Fact]
    public void Encode_then_TryDecode_recovers_empty_string()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var encoded = ring.Encode(string.Empty);

        var status = ring.TryDecode(encoded, out string decoded);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal(string.Empty, decoded);
    }

    [Fact]
    public void Encode_then_TryDecode_recovers_utf8_multibyte()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        const string value = "zażółć gęślą jaźń — 日本語 — 🔐";
        var encoded = ring.Encode(value);

        var status = ring.TryDecode(encoded, out string decoded);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal(value, decoded);
    }

    [Fact]
    public void Encode_then_TryDecode_recovers_large_heap_path_value()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var value = new string('x', 4096);
        var encoded = ring.Encode(value);

        var status = ring.TryDecode(encoded, out string decoded);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal(value, decoded);
    }

    [Fact]
    public void Encode_produces_value_with_reserved_prefix()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var encoded = ring.Encode("anything");

        Assert.StartsWith(EphemeralKeyRing.ReservedPrefix, encoded.Encoded);
    }

    [Fact]
    public void Encode_same_value_twice_produces_different_ciphertext()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var first = ring.Encode("same");
        var second = ring.Encode("same");

        Assert.NotEqual(first.Encoded, second.Encoded);
    }

    [Fact]
    public void Encode_bytes_produces_decodable_ephemeral_value()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var payload = RandomNumberGenerator.GetBytes(40);
        var encoded = ring.Encode(payload);

        Assert.StartsWith(EphemeralKeyRing.ReservedPrefix, encoded.Encoded);

        var status = ring.TryDecryptBytes(FrameOf(encoded), out var recovered);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal(payload, recovered);
    }

    [Fact]
    public void Encode_bytes_then_TryDecode_recovers_bytes()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var payload = RandomNumberGenerator.GetBytes(64);
        var encoded = ring.Encode(payload);

        var status = ring.TryDecode(encoded, out byte[] recovered);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal(payload, recovered);
    }

    [Fact]
    public void TryDecode_bytes_returns_Expired_after_epoch_shredded()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var encoded = ring.Encode(RandomNumberGenerator.GetBytes(16));

        clock.Advance(TimeSpan.FromHours(25) + TimeSpan.FromMinutes(1));
        ring.SweepExpired();

        var status = ring.TryDecode(encoded, out byte[] recovered);

        Assert.Equal(EphemeralDecodeStatus.Expired, status);
        Assert.Empty(recovered);
    }

    // ---- EncodedEphemeralValue marker type ----

    [Fact]
    public void EncodedEphemeralValue_rejects_value_without_prefix()
    {
        Assert.Throws<ArgumentException>(
            () => new EncodedEphemeralValue("not-an-ephemeral"));
    }

    [Fact]
    public void EncodedEphemeralValue_rejects_metadata_prefix()
    {
        Assert.Throws<ArgumentException>(
            () => new EncodedEphemeralValue("pse:smuggled"));
    }

    [Fact]
    public void EncodedEphemeralValue_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EncodedEphemeralValue(null!));
    }

    [Fact]
    public void EncodedEphemeralValue_rejects_empty()
    {
        Assert.Throws<ArgumentException>(
            () => new EncodedEphemeralValue(string.Empty));
    }

    [Fact]
    public void EncodedEphemeralValue_accepts_prefixed_value()
    {
        var value = new EncodedEphemeralValue("eph:AAAA");

        Assert.Equal("eph:AAAA", value.Encoded);
    }

    [Fact]
    public void EncodedEphemeralValue_ToString_does_not_leak_ciphertext()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var encoded = ring.Encode("secret-value");

        Assert.DoesNotContain("secret", encoded.ToString());
        Assert.DoesNotContain(encoded.Encoded, encoded.ToString());
    }

    // ---- TryDecode status scenarios ----

    [Fact]
    public void TryDecode_returns_Expired_after_epoch_shredded()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var encoded = ring.Encode("ttl-bound");

        clock.Advance(TimeSpan.FromHours(25) + TimeSpan.FromMinutes(1));
        ring.SweepExpired();

        var status = ring.TryDecode(encoded, out string decoded);

        Assert.Equal(EphemeralDecodeStatus.Expired, status);
        Assert.Equal(string.Empty, decoded);
    }

    [Fact]
    public void TryDecode_returns_DecryptionFailed_for_tampered_ciphertext()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var encoded = ring.Encode("tamper-me");
        var frame = FrameOf(encoded);
        frame[^1] ^= 0xFF;

        var status = ring.TryDecode(Wrap(frame), out string _);

        Assert.Equal(EphemeralDecodeStatus.DecryptionFailed, status);
    }

    [Fact]
    public void TryDecode_returns_DecryptionFailed_for_tampered_tag()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var encoded = ring.Encode("tag-tamper");
        var frame = FrameOf(encoded);
        frame[1 + 8 + 12] ^= 0xFF;

        var status = ring.TryDecode(Wrap(frame), out string _);

        Assert.Equal(EphemeralDecodeStatus.DecryptionFailed, status);
    }

    [Fact]
    public void TryDecode_returns_DecryptionFailed_for_malformed_base64()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var status = ring.TryDecode(
            new EncodedEphemeralValue("eph:!!!not-base64!!!"),
            out string _);

        Assert.Equal(EphemeralDecodeStatus.DecryptionFailed, status);
    }

    [Fact]
    public void TryDecode_returns_DecryptionFailed_for_default_value()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var status = ring.TryDecode(default, out string _);

        Assert.Equal(EphemeralDecodeStatus.DecryptionFailed, status);
    }

    // ---- Byte-level core ----

    [Fact]
    public void EncryptBytes_then_TryDecryptBytes_roundtrip()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var payload = RandomNumberGenerator.GetBytes(48);
        var frame = ring.EncryptBytes(payload);

        var status = ring.TryDecryptBytes(frame, out var recovered);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal(payload, recovered);
    }

    [Fact]
    public void TryDecryptBytes_returns_DecryptionFailed_for_too_short_frame()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var status = ring.TryDecryptBytes(new byte[8], out _);

        Assert.Equal(EphemeralDecodeStatus.DecryptionFailed, status);
    }

    [Fact]
    public void TryDecryptBytes_returns_DecryptionFailed_for_unknown_version()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var frame = ring.EncryptBytes(RandomNumberGenerator.GetBytes(16));
        frame[0] = 0x02;

        var status = ring.TryDecryptBytes(frame, out _);

        Assert.Equal(EphemeralDecodeStatus.DecryptionFailed, status);
    }

    [Fact]
    public void TryDecryptBytes_returns_Expired_for_unknown_epoch()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var frame = ring.EncryptBytes(RandomNumberGenerator.GetBytes(16));

        clock.Advance(TimeSpan.FromHours(25) + TimeSpan.FromMinutes(1));
        ring.SweepExpired();

        var status = ring.TryDecryptBytes(frame, out _);

        Assert.Equal(EphemeralDecodeStatus.Expired, status);
    }

    // ---- Rotation & TTL ----

    [Fact]
    public void Encrypting_within_rotation_interval_reuses_current_epoch()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var first = ring.Encode("a");
        clock.Advance(TimeSpan.FromMinutes(30));
        var second = ring.Encode("b");

        Assert.Equal(EpochIdOf(first), EpochIdOf(second));
    }

    [Fact]
    public void Encrypting_after_rotation_interval_creates_new_epoch()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var first = ring.Encode("a");
        clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        var second = ring.Encode("b");

        Assert.NotEqual(EpochIdOf(first), EpochIdOf(second));
    }

    [Fact]
    public void Old_epoch_remains_decodable_after_rotation()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var first = ring.Encode("old");
        clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        ring.Encode("new");

        var status = ring.TryDecode(first, out string decoded);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal("old", decoded);
    }

    [Fact]
    public void Value_remains_decodable_for_at_least_min_lifetime()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        ring.Encode("warm-up");
        clock.Advance(TimeSpan.FromMinutes(59));

        var value = ring.Encode("payload");

        clock.Advance(TimeSpan.FromHours(24));
        ring.SweepExpired();

        var status = ring.TryDecode(value, out string decoded);

        Assert.Equal(EphemeralDecodeStatus.Ok, status);
        Assert.Equal("payload", decoded);
    }

    [Fact]
    public void Value_expires_after_full_lifetime()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var value = ring.Encode("payload");

        clock.Advance(TimeSpan.FromHours(25) + TimeSpan.FromMinutes(1));
        ring.SweepExpired();

        var status = ring.TryDecode(value, out string _);

        Assert.Equal(EphemeralDecodeStatus.Expired, status);
    }

    [Fact]
    public void SweepExpired_removes_only_expired_epochs()
    {
        var clock = new MutableClock();
        using var ring = CreateRing(clock);

        var old = ring.Encode("old");
        clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        var fresh = ring.Encode("fresh");

        clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(30));
        ring.SweepExpired();

        Assert.Equal(EphemeralDecodeStatus.Expired, ring.TryDecode(old, out string _));
        Assert.Equal(EphemeralDecodeStatus.Ok, ring.TryDecode(fresh, out string _));
    }

    // ---- Disposal ----

    [Fact]
    public void EncryptBytes_after_dispose_throws()
    {
        var clock = new MutableClock();
        var ring = CreateRing(clock);
        ring.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => ring.EncryptBytes(new byte[8]));
    }

    [Fact]
    public void TryDecode_after_dispose_throws()
    {
        var clock = new MutableClock();
        var ring = CreateRing(clock);
        var encoded = ring.Encode("x");
        ring.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => ring.TryDecode(encoded, out string _));
    }

    private sealed class MutableClock : IClock
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public DateTimeOffset UtcNow => _utcNow;
        public DateTime Now => _utcNow.UtcDateTime;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
