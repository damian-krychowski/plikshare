using System.Collections.Concurrent;
using PlikShare.Core.Clock;
using Serilog;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Process-local store for pre-derived file-scoped encryption inputs handed off from an
/// authenticated HTTP request to background queue work.
///
/// <para>Each entry is a <see cref="Package"/> containing an optional decryption input (used
/// to decrypt one existing file — e.g. a thumbnail's parent) plus zero-or-more encryption
/// inputs (used to encrypt new files the job will produce — e.g. each thumbnail variant).
/// The wires are <see cref="FileAesInputsV2Wire"/>: the file keys are AES-GCM ciphertext
/// under the process master key, so a heap scrape of the store on its own yields nothing
/// usable.</para>
///
/// <para>Encryption inputs are one-shot: each <see cref="Package.TakeNextEncryptionInput"/>
/// consumes the next wire and exhausting the package throws — a positive signal that the
/// worker asked for more outputs than the trigger time provisioned. The decryption input
/// follows the same one-shot discipline via <see cref="Package.TakeDecryptionInput"/>.</para>
///
/// <para>The store does NOT sweep on its own. Pair it with a hosted background sweeper that
/// calls <see cref="SweepExpired"/> on a fixed cadence. Default TTL is 24 hours.</para>
///
/// <para>Lifecycle is process-scoped — entries do not survive a restart. Queue jobs whose
/// handle no longer resolves should fail cleanly and let the user retrigger.</para>
/// </summary>
public sealed class TemporaryEncryptionStore : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<TemporaryEncryptionStore>();

    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly IClock _clock;
    private volatile bool _disposed;

    public TemporaryEncryptionStore(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Stores a package of encryption inputs and returns a handle the queue payload can carry.
    /// At least one of <paramref name="decryptionInput"/> or <paramref name="encryptionInputs"/>
    /// should be non-empty — an empty package is legal but useless.
    /// </summary>
    public Guid Store(
        FileAesInputsV2Wire? decryptionInput,
        IReadOnlyList<FileAesInputsV2Wire> encryptionInputs,
        TimeSpan? ttl = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(encryptionInputs);

        var package = new Package(
            decryptionInput: decryptionInput,
            encryptionInputs: encryptionInputs);

        var id = Guid.NewGuid();
        var expiresAt = _clock.UtcNow.Add(ttl ?? DefaultTtl);

        _entries[id] = new Entry(
            package: package,
            expiresAt: expiresAt);

        Logger.Debug(
            "Stored temporary file encryption inputs {Id} (hasDecryptionInput: {HasDecryptionInput}, encryptionInputs: {EncryptionInputsCount}, expires: {ExpiresAt:O})",
            id,
            decryptionInput is not null,
            encryptionInputs.Count,
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

        if (entry.ExpiresAt <= _clock.UtcNow)
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

        var now = _clock.UtcNow;
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
        private readonly FileAesInputsV2Wire? _decryptionInput;
        private readonly FileAesInputsV2Wire[] _encryptionInputs;
        private int _decryptionInputTaken;
        private int _nextEncryptionInputIndex;

        public Package(
            FileAesInputsV2Wire? decryptionInput,
            IReadOnlyList<FileAesInputsV2Wire> encryptionInputs)
        {
            ArgumentNullException.ThrowIfNull(encryptionInputs);

            _decryptionInput = decryptionInput;
            // Defensive copy — the caller's list is theirs to mutate, the package's slot count
            // (EncryptionInputsCount) must be a stable invariant for the lifetime of this entry.
            _encryptionInputs = encryptionInputs.ToArray();
        }

        public bool HasDecryptionInput => _decryptionInput is not null;
        public int EncryptionInputsCount => _encryptionInputs.Length;

        /// <summary>How many encryption inputs are still available to <see cref="TakeNextEncryptionInput"/>.</summary>
        public int EncryptionInputsRemaining =>
            Math.Max(0, _encryptionInputs.Length - Volatile.Read(ref _nextEncryptionInputIndex));

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
    }
}
