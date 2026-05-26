using System.Collections.Concurrent;
using PlikShare.Core.Clock;
using Serilog;

namespace PlikShare.Core.Encryption;

/// <summary>
/// In-memory, process-local store for short-lived <see cref="WorkspaceEncryptionSession"/>
/// instances handed off from an authenticated HTTP request to a background queue worker.
///
/// <para>The problem: full-encrypted workspaces unwrap their Workspace DEKs from the user's
/// session cookie at request time. Queue jobs run asynchronously without that cookie. We can
/// neither serialize the DEK to the queue payload (the queue row lives in SQLite, on disk)
/// nor leave the request-bound session alive past the response — the framework disposes it
/// with the HTTP response.</para>
///
/// <para>The solution: at trigger time the caller hands us a session, we CLONE every
/// <see cref="WorkspaceDekEntry.Dek"/> via <see cref="SecureBytes.Clone"/> into a fresh
/// session owned by us, return a Guid handle, and embed only the handle in the queue payload.
/// The worker resolves the handle back to the session, runs its job, and removes the entry.
/// If the process restarts or the TTL elapses before the worker picks up, the handle becomes
/// orphan and the job fails cleanly — the user retriggers.</para>
///
/// <para>Hard rules:
/// <list type="bullet">
///   <item>DEKs never touch disk (no DB column, no log line, no queue payload).</item>
///   <item>Entries are <see cref="WorkspaceEncryptionSession"/> instances we own — disposed
///   on <see cref="Remove"/>, on TTL sweep, or on application shutdown — which in turn zero
///   their <see cref="SecureBytes"/> and unlock the mlocked pages.</item>
///   <item>The store is process-scoped; a multi-instance deployment must route the queue job
///   back to the same instance, or the handle won't resolve. For PlikShare's single-process
///   queue worker this is fine.</item>
/// </list></para>
/// </summary>
public sealed class TemporaryWorkspaceEncryptionKeyStore : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<TemporaryWorkspaceEncryptionKeyStore>();
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly IClock _clock;
    private readonly Timer _sweeper;
    private volatile bool _disposed;

    public TemporaryWorkspaceEncryptionKeyStore(IClock clock)
    {
        _clock = clock;
        _sweeper = new Timer(
            callback: _ => SweepExpired(),
            state: null,
            dueTime: SweepInterval,
            period: SweepInterval);
    }

    /// <summary>
    /// Stores a clone of the given session's DEKs and returns a handle the queue payload
    /// can carry. The original session is untouched — caller still owns it (typically the
    /// HTTP request will dispose it on response).
    /// </summary>
    public Guid Store(
        WorkspaceEncryptionSession source,
        TimeSpan? ttl = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);

        var clonedEntries = new WorkspaceDekEntry[source.Entries.Length];

        try
        {
            for (var i = 0; i < source.Entries.Length; i++)
            {
                clonedEntries[i] = new WorkspaceDekEntry
                {
                    StorageDekVersion = source.Entries[i].StorageDekVersion,
                    Dek = source.Entries[i].Dek.Clone()
                };
            }
        }
        catch
        {
            // Roll back already-cloned DEKs before bubbling the failure.
            foreach (var e in clonedEntries)
                e?.Dispose();

            throw;
        }

        var clonedSession = new WorkspaceEncryptionSession(
            workspaceId: source.WorkspaceId,
            entries: clonedEntries);

        var id = Guid.NewGuid();
        var expiresAt = _clock.UtcNow.Add(ttl ?? DefaultTtl);

        _entries[id] = new Entry(clonedSession, expiresAt);

        Logger.Debug(
            "Stored temporary workspace encryption key Workspace#{WorkspaceId} → {KeyId} (expires {ExpiresAt:O})",
            source.WorkspaceId,
            id,
            expiresAt);

        return id;
    }

    /// <summary>
    /// Returns the session for the given handle if it still exists and hasn't expired.
    /// The returned session is owned by the store — caller MUST NOT dispose it; instead
    /// call <see cref="Remove"/> when done.
    /// </summary>
    public WorkspaceEncryptionSession? TryRetrieve(Guid id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_entries.TryGetValue(id, out var entry))
            return null;

        if (entry.ExpiresAt <= _clock.UtcNow)
        {
            // Expired between sweeps — treat as missing and clean up here too.
            if (_entries.TryRemove(id, out var removed))
                removed.Dispose();

            return null;
        }

        return entry.Session;
    }

    public void Remove(Guid id)
    {
        if (_disposed) return;

        if (_entries.TryRemove(id, out var entry))
        {
            entry.Dispose();
            Logger.Debug("Removed temporary workspace encryption key {KeyId}", id);
        }
    }

    private void SweepExpired()
    {
        if (_disposed) return;

        var now = _clock.UtcNow;
        var swept = 0;

        foreach (var (id, entry) in _entries)
        {
            if (entry.ExpiresAt > now) continue;

            if (_entries.TryRemove(id, out var removed))
            {
                removed.Dispose();
                swept++;
            }
        }

        if (swept > 0)
            Logger.Debug("Swept {Count} expired temporary workspace encryption keys", swept);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _sweeper.Dispose();

        foreach (var entry in _entries.Values)
            entry.Dispose();

        _entries.Clear();
    }

    private sealed class Entry : IDisposable
    {
        public WorkspaceEncryptionSession Session { get; }
        public DateTimeOffset ExpiresAt { get; }

        public Entry(WorkspaceEncryptionSession session, DateTimeOffset expiresAt)
        {
            Session = session;
            ExpiresAt = expiresAt;
        }

        public void Dispose()
        {
            Session.Dispose();
        }
    }
}
