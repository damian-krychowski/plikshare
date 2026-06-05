using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Clock;
using Serilog;

namespace PlikShare.Core.Encryption;

public sealed class EphemeralKeyRing(
    IClock clock,
    EphemeralKeyRingOptions options) : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<EphemeralKeyRing>();

    public const string ReservedPrefix = "eph:";

    private const byte FormatVersionV1 = 0x01;
    private const int FormatVersionSize = 1;
    private const int EpochIdSize = 8;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private const int HeaderSize =
        FormatVersionSize + EpochIdSize + NonceSize + TagSize;

    private const int StackAllocThresholdBytes = 512;

    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly Dictionary<ulong, Epoch> _epochs = new();
    private Epoch? _current;
    private ulong _nextEpochId = 1;
    private bool _disposed;

    public byte[] EncryptBytes(ReadOnlySpan<byte> plaintext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var output = new byte[HeaderSize + plaintext.Length];

        _lock.EnterUpgradeableReadLock();

        try
        {
            var epoch = GetOrCreateCurrent();

            output[0] = FormatVersionV1;

            BinaryPrimitives.WriteUInt64BigEndian(
                output.AsSpan(FormatVersionSize, EpochIdSize),
                epoch.Id);

            var nonce = output.AsSpan(
                FormatVersionSize + EpochIdSize,
                NonceSize);

            var tag = output.AsSpan(
                FormatVersionSize + EpochIdSize + NonceSize,
                TagSize);

            var ciphertext = output.AsSpan(
                HeaderSize,
                plaintext.Length);

            RandomNumberGenerator.Fill(nonce);

            epoch.Key.Use(
                state: new EncryptState
                {
                    Nonce = nonce,
                    Tag = tag,
                    Plaintext = plaintext,
                    Ciphertext = ciphertext
                },
                action: static (keySpan, s) =>
                {
                    using var aes = new AesGcm(keySpan, TagSize);

                    aes.Encrypt(
                        nonce: s.Nonce,
                        plaintext: s.Plaintext,
                        ciphertext: s.Ciphertext,
                        tag: s.Tag);
                });
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }

        return output;
    }

    public EphemeralDecodeStatus TryDecryptBytes(ReadOnlySpan<byte> frame, out byte[] plaintext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        plaintext = [];

        if (frame.Length < HeaderSize)
            return EphemeralDecodeStatus.DecryptionFailed;

        if (frame[0] != FormatVersionV1)
            return EphemeralDecodeStatus.DecryptionFailed;

        var epochId = BinaryPrimitives.ReadUInt64BigEndian(
            frame.Slice(FormatVersionSize, EpochIdSize));

        var nonce = frame.Slice(
            FormatVersionSize + EpochIdSize,
            NonceSize);

        var tag = frame.Slice(
            FormatVersionSize + EpochIdSize + NonceSize,
            TagSize);

        var ciphertext = frame[HeaderSize..];
        var output = new byte[ciphertext.Length];

        _lock.EnterReadLock();

        try
        {
            if (!_epochs.TryGetValue(epochId, out var epoch))
                return EphemeralDecodeStatus.Expired;

            epoch.Key.Use(
                state: new DecryptState
                {
                    Nonce = nonce,
                    Tag = tag,
                    Ciphertext = ciphertext,
                    Plaintext = output
                },
                action: static (keySpan, s) =>
                {
                    using var aes = new AesGcm(keySpan, TagSize);

                    aes.Decrypt(
                        nonce: s.Nonce,
                        ciphertext: s.Ciphertext,
                        tag: s.Tag,
                        plaintext: s.Plaintext);
                });
        }
        catch (CryptographicException)
        {
            return EphemeralDecodeStatus.DecryptionFailed;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        plaintext = output;
        return EphemeralDecodeStatus.Ok;
    }

    public EncodedEphemeralValue Encode(ReadOnlySpan<byte> value)
    {
        var frame = EncryptBytes(value);

        return new EncodedEphemeralValue(
            string.Concat(
                ReservedPrefix,
                Convert.ToBase64String(frame)));
    }

    public EncodedEphemeralValue Encode(string value)
    {
        var utf8Length = Encoding.UTF8.GetByteCount(value);

        var rented = utf8Length > StackAllocThresholdBytes
            ? ArrayPool<byte>.Shared.Rent(utf8Length)
            : null;

        try
        {
            var plaintext = rented is null
                ? stackalloc byte[utf8Length]
                : rented.AsSpan(0, utf8Length);

            Encoding.UTF8.GetBytes(value, plaintext);

            return Encode(plaintext);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public EphemeralDecodeStatus TryDecode(EncodedEphemeralValue encoded, out byte[] value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        value = [];

        var raw = encoded.Encoded;

        if (string.IsNullOrEmpty(raw)
            || !raw.StartsWith(ReservedPrefix, StringComparison.Ordinal))
            return EphemeralDecodeStatus.DecryptionFailed;

        var base64Length = raw.Length - ReservedPrefix.Length;
        var maxFrameLength = base64Length / 4 * 3;

        if (maxFrameLength < HeaderSize)
            return EphemeralDecodeStatus.DecryptionFailed;

        var rented = maxFrameLength > StackAllocThresholdBytes
            ? ArrayPool<byte>.Shared.Rent(maxFrameLength)
            : null;

        try
        {
            var frameBuffer = rented is null
                ? stackalloc byte[maxFrameLength]
                : rented.AsSpan(0, maxFrameLength);

            if (!Convert.TryFromBase64Chars(
                    raw.AsSpan(ReservedPrefix.Length),
                    frameBuffer,
                    out var frameLength))
                return EphemeralDecodeStatus.DecryptionFailed;

            return TryDecryptBytes(
                frameBuffer[..frameLength],
                out value);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public EphemeralDecodeStatus TryDecode(EncodedEphemeralValue encoded, out string value)
    {
        value = string.Empty;

        var status = TryDecode(
            encoded, 
            out byte[] plaintext);

        if (status != EphemeralDecodeStatus.Ok)
            return status;

        value = Encoding.UTF8.GetString(plaintext);
        return EphemeralDecodeStatus.Ok;
    }

    public void SweepExpired()
    {
        if (_disposed)
            return;

        var now = clock.UtcNow;

        _lock.EnterWriteLock();

        try
        {
            var expired = new List<ulong>();

            foreach (var (id, epoch) in _epochs)
            {
                if (epoch.ExpiresAt <= now)
                    expired.Add(id);
            }

            foreach (var id in expired)
            {
                if (!_epochs.Remove(id, out var epoch))
                    continue;

                if (ReferenceEquals(_current, epoch))
                    _current = null;

                epoch.Dispose();
            }

            if (expired.Count > 0)
                Logger.Debug(
                    "Shredded {Count} expired ephemeral epochs",
                    expired.Count);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _lock.EnterWriteLock();

        try
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var epoch in _epochs.Values)
                epoch.Dispose();

            _epochs.Clear();
            _current = null;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
    }

    private Epoch GetOrCreateCurrent()
    {
        var now = clock.UtcNow;

        if (_current is { } current && now - current.CreatedAt < options.RotationInterval)
            return current;

        _lock.EnterWriteLock();

        try
        {
            if (_current is { } latest && now - latest.CreatedAt < options.RotationInterval)
                return latest;

            var epoch = CreateEpoch(now);

            _epochs[epoch.Id] = epoch;
            _current = epoch;

            Logger.Debug(
                "Rotated ephemeral key ring to epoch {EpochId}, expires at {ExpiresAt}",
                epoch.Id,
                epoch.ExpiresAt);

            return epoch;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private Epoch CreateEpoch(DateTimeOffset now)
    {
        var id = _nextEpochId++;

        var key = SecureBytes.Create(
            length: KeySize,
            state: 0,
            initializer: static (span, _) => RandomNumberGenerator.Fill(span));

        return new Epoch(
            id: id,
            key: key,
            createdAt: now,
            expiresAt: now.Add(options.Lifetime));
    }

    private sealed class Epoch(
        ulong id,
        SecureBytes key,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) : IDisposable
    {
        public ulong Id { get; } = id;
        public SecureBytes Key { get; } = key;
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public void Dispose() => Key.Dispose();
    }

    private readonly ref struct EncryptState
    {
        public Span<byte> Nonce { get; init; }
        public Span<byte> Tag { get; init; }
        public ReadOnlySpan<byte> Plaintext { get; init; }
        public Span<byte> Ciphertext { get; init; }
    }

    private readonly ref struct DecryptState
    {
        public ReadOnlySpan<byte> Nonce { get; init; }
        public ReadOnlySpan<byte> Tag { get; init; }
        public ReadOnlySpan<byte> Ciphertext { get; init; }
        public Span<byte> Plaintext { get; init; }
    }
}

public enum EphemeralDecodeStatus
{
    Ok = 0,
    DecryptionFailed,
    Expired
}

public sealed class EphemeralKeyRingOptions
{
    public TimeSpan MinLifetime { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan RotationInterval { get; init; } = TimeSpan.FromHours(1);
    public TimeSpan Lifetime => MinLifetime + RotationInterval;

    public int SweepIntervalSeconds { get; init; } = 300;
    public int StartupGraceSeconds { get; init; } = 5;
}
