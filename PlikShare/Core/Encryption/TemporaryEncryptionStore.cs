using System.Collections.Concurrent;
using PlikShare.Core.Clock;
using Serilog;

namespace PlikShare.Core.Encryption;

//i don't have better idea now how to approach this problem for full-encrypted storages...
public sealed class TemporaryEncryptionStore(IClock clock) : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<TemporaryEncryptionStore>();

    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private volatile bool _disposed;

    /// <summary>
    /// Stores a package of encryption inputs and returns a handle the queue payload can carry.
    /// At least one of <paramref name="decryptionInput"/> / <paramref name="encryptionInputs"/>
    /// / <paramref name="metadataEncryptionSeed"/> should be non-null/non-empty — an empty
    /// package is legal but useless.
    /// </summary>
    public Guid Store(
        FileAesInputsV2Wire? decryptionInput,
        IReadOnlyList<FileAesInputsV2Wire> encryptionInputs,
        EncryptionSeedWire? metadataEncryptionSeed,
        TimeSpan? ttl = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(encryptionInputs);

        var package = new Package(
            decryptionInput: decryptionInput,
            encryptionInputs: encryptionInputs,
            metadataEncryptionSeed: metadataEncryptionSeed);

        var id = Guid.NewGuid();
        var expiresAt = clock.UtcNow.Add(ttl ?? DefaultTtl);

        _entries[id] = new Entry(
            package: package,
            expiresAt: expiresAt);

        Logger.Debug(
            "Stored temporary file encryption inputs {Id} (hasDecryptionInput: {HasDecryptionInput}, encryptionInputs: {EncryptionInputsCount}, hasMetadataSeed: {HasMetadataSeed}, expires: {ExpiresAt:O})",
            id,
            decryptionInput is not null,
            encryptionInputs.Count,
            metadataEncryptionSeed is not null,
            expiresAt);

        return id;
    }

    /// <summary>
    /// Returns the package for the given handle if it exists and has not expired. The returned
    /// package remains owned by the store — call <see cref="Remove"/> when the job is done so
    /// the entry does not linger to TTL.
    /// </summary>
    public Package? TryRetrieve(Guid id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_entries.TryGetValue(id, out var entry))
            return null;

        if (entry.ExpiresAt <= clock.UtcNow)
        {
            // Expired between sweeps — clean up here too so the next caller sees a clean miss.
            _entries.TryRemove(id, out _);
            return null;
        }

        return entry.Package;
    }

    public void Remove(Guid id)
    {
        if (_disposed) return;

        if (_entries.TryRemove(id, out _))
            Logger.Debug("Removed temporary file encryption inputs {Id}", id);
    }

    /// <summary>
    /// Drops every entry whose TTL has elapsed. Cheap, lock-free; safe to call concurrently
    /// with <see cref="Store"/> / <see cref="TryRetrieve"/> / <see cref="Remove"/>.
    /// </summary>
    public void SweepExpired()
    {
        if (_disposed) return;

        var now = clock.UtcNow;
        var swept = 0;

        foreach (var (id, entry) in _entries)
        {
            if (entry.ExpiresAt > now) continue;

            if (_entries.TryRemove(id, out _))
                swept++;
        }

        if (swept > 0)
            Logger.Debug("Swept {Count} expired temporary file encryption inputs", swept);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _entries.Clear();
    }

    private sealed class Entry
    {
        public Package Package { get; }
        public DateTimeOffset ExpiresAt { get; }

        public Entry(Package package, DateTimeOffset expiresAt)
        {
            Package = package;
            ExpiresAt = expiresAt;
        }
    }

    /// <summary>
    /// A bundle of encryption inputs provisioned at trigger time for one queue job. The
    /// decryption input is taken at most once (to decrypt the existing source file); encryption
    /// inputs are taken in order, each at most once (to encrypt the files the worker produces).
    /// Both contracts trip an exception on misuse so a runaway worker requesting more outputs
    /// than were provisioned fails fast rather than silently re-using a key.
    /// </summary>
    public sealed class Package
    {
        private readonly FileAesInputsV2Wire _decryptionInput;
        private readonly FileAesInputsV2Wire[] _encryptionInputs;
        private readonly EncryptionSeedWire _metadataEncryptionSeed;
        private int _decryptionInputTaken;
        private int _nextEncryptionInputIndex;
        private int _metadataEncryptionSeedTaken;

        public Package(
            FileAesInputsV2Wire decryptionInput,
            IReadOnlyList<FileAesInputsV2Wire> encryptionInputs,
            EncryptionSeedWire metadataEncryptionSeed)
        {
            ArgumentNullException.ThrowIfNull(encryptionInputs);

            _decryptionInput = decryptionInput;
            // Defensive copy — the caller's list is theirs to mutate, the package's slot count
            // (EncryptionInputsCount) must be a stable invariant for the lifetime of this entry.
            _encryptionInputs = encryptionInputs.ToArray();
            _metadataEncryptionSeed = metadataEncryptionSeed;
        }
        
        /// <summary>
        /// Returns the decryption input and marks it consumed. Throws if there is no decryption
        /// input or if it was already taken — both are programmer errors, not recoverable
        /// runtime states.
        /// </summary>
        public FileAesInputsV2Wire TakeDecryptionInput()
        {
            if (_decryptionInput is null)
                throw new InvalidOperationException(
                    "This package has no decryption input — caller asked for one where none was provisioned at trigger time.");

            if (Interlocked.Exchange(ref _decryptionInputTaken, 1) != 0)
                throw new InvalidOperationException(
                    "Decryption input has already been taken from this package — single-use violated.");

            return _decryptionInput;
        }

        /// <summary>
        /// Returns the next unused encryption input and advances the cursor. Throws once the
        /// package is exhausted — by design: the worker should ask for exactly as many
        /// encryptions as the trigger time provisioned, and a mismatch is a bug to surface.
        /// </summary>
        public FileAesInputsV2Wire TakeNextEncryptionInput()
        {
            // Increment first, then check — if two threads race, each gets a distinct index
            // and at most one of them sees an index past the end (which then throws). No two
            // threads ever receive the same wire.
            var oneBasedIndex = Interlocked.Increment(ref _nextEncryptionInputIndex);

            if (oneBasedIndex > _encryptionInputs.Length)
                throw new InvalidOperationException(
                    $"All {_encryptionInputs.Length} encryption inputs have already been taken from this package — requested {oneBasedIndex}-th.");

            return _encryptionInputs[oneBasedIndex - 1];
        }

        /// <summary>
        /// Returns the metadata encryption seed wire and marks it consumed. The worker is
        /// expected to unwrap this once via <see cref="EncryptionSeedWire.Unwrap"/> and reuse the
        /// resulting <see cref="EncryptionSeed"/> across every metadata field it encrypts in
        /// this job — each <c>MetadataAesInputsV1.Prepare(seed)</c> generates a fresh per-value
        /// salt internally. Throws if there is no seed or if it was already taken.
        /// </summary>
        public EncryptionSeedWire TakeMetadataEncryptionSeed()
        {
            if (_metadataEncryptionSeed is null)
                throw new InvalidOperationException(
                    "This package has no metadata encryption seed — caller asked for one where none was provisioned at trigger time.");

            if (Interlocked.Exchange(ref _metadataEncryptionSeedTaken, 1) != 0)
                throw new InvalidOperationException(
                    "Metadata encryption seed has already been taken from this package — single-use violated.");

            return _metadataEncryptionSeed;
        }
    }
}
