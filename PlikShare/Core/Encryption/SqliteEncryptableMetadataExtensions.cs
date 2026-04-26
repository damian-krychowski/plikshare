using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Serilog;

namespace PlikShare.Core.Encryption;

/// <summary>
/// SQLite UDF that decodes encryptable-metadata values inline during query execution.
///
/// Per row:
///   - NULL or non-string         → NULL passthrough.
///   - non-pse string (plaintext) → returned verbatim, no work done.
///   - pse: envelope              → decoded via the workspace session looked up by the
///                                  second argument (the row's workspace id).
///
/// On any data-level failure (no session for the workspace, wrong key version, AES tag
/// mismatch, malformed envelope) the function returns NULL — these are per-row errors
/// that must not abort the entire SQL statement.
///
/// On a scope-lifetime failure (the UDF was invoked after its <see cref="DecryptMetadataFunctionScope"/>
/// was disposed), the function throws and SQLite aborts the statement loudly. That branch
/// signals a programming error: the UDF leaked past its intended per-request lifetime.
///
/// <para>
/// Safety layers stacked on the UDF:
/// </para>
/// <list type="number">
///   <item><description>
///     Pair with <c>PlikShareDb.OpenNonPooledConnection</c> so the connection (and the
///     UDF binding glued to it) cannot be observed by a different request that draws
///     the same connection from the pool.
///   </description></item>
///   <item><description>
///     The registration returns a <see cref="DecryptMetadataFunctionScope"/>. Disposing
///     the scope flips a volatile flag and nulls the captured session reference.
///     Subsequent UDF calls go down the throw path even if the connection is somehow
///     still alive — defense-in-depth against scope-lifetime bugs.
///   </description></item>
/// </list>
///
/// Both the connection and the scope must live no longer than the work that owns the
/// session map. Idiomatic call site: <c>using var connection = ...; using var scope = connection.RegisterDecryptMetadataFunction(sessions);</c>
/// </summary>
public static class SqliteEncryptableMetadataExtensions
{
    public const string DecryptFunctionName = "app_decrypt_metadata";

    public static DecryptMetadataFunctionScope RegisterDecryptMetadataFunction(
        this SqliteConnection connection,
        IReadOnlyDictionary<int, WorkspaceEncryptionSession> sessionsByWorkspaceId)
    {
        var scope = new DecryptMetadataFunctionScope(sessionsByWorkspaceId);

        connection.CreateFunction(
            DecryptFunctionName,
            (string? value, long workspaceId) =>
            {
                // Disposed-check first, before any other branch — even passthrough of a
                // plain string must fail loudly post-dispose. Throwing here propagates
                // back through Microsoft.Data.Sqlite and aborts the SQL statement, which
                // is the intended outcome: a UDF call after dispose means something is
                // using the connection past its expected lifetime, a bug.
                scope.ThrowIfDisposed();

                if (value is null)
                    return null;

                if (!value.StartsWith(EncryptableMetadataExtensions.ReservedPrefix, StringComparison.Ordinal))
                    return value;

                if (!scope.TryGetWorkspaceSession((int)workspaceId, out var session))
                    return null;

                try
                {
                    return session.DecodeEncryptableMetadata(value);
                }
                catch (Exception e)
                {
                    Log.Warning(e,
                        "{Function} failed to decode metadata for Workspace#{WorkspaceId}",
                        DecryptFunctionName, workspaceId);

                    return null;
                }
            });

        return scope;
    }
}

/// <summary>
/// Lifetime token for an <c>app_decrypt_metadata</c> registration. After dispose every
/// UDF call throws and SQLite aborts the statement.
/// </summary>
public sealed class DecryptMetadataFunctionScope : IDisposable
{
    private readonly IReadOnlyDictionary<int, WorkspaceEncryptionSession> _sessions;
    private volatile bool _disposed;

    internal DecryptMetadataFunctionScope(
        IReadOnlyDictionary<int, WorkspaceEncryptionSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions = sessions;
    }

    public void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Looks up the workspace session. Throws on a disposed scope. Returns <c>false</c>
    /// when the scope is live but the workspace has no session in the map — that is a
    /// normal "no key, skip row" case for the UDF.
    /// </summary>
    internal bool TryGetWorkspaceSession(
        int workspaceId,
        out WorkspaceEncryptionSession? session)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _sessions.TryGetValue(workspaceId, out session);
    }

    public void Dispose() => _disposed = true;
}
