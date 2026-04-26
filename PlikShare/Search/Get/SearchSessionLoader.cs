using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Search.Get;

/// <summary>
/// Builds the per-request map of <see cref="WorkspaceEncryptionSession"/>s used by the
/// encrypted-search SQL path. The map is keyed by workspace id so the
/// <c>app_decrypt_metadata</c> UDF can look up the right DEK while scanning rows.
///
/// Only workspaces the caller can actually decrypt are included. Decryption capability
/// is read directly from <see cref="UserContext.WrappedWorkspaceDeks"/> (per-workspace
/// wraps) and <see cref="UserContext.WrappedStorageDeks"/> (per-storage wraps from which
/// a Workspace DEK can be derived). Workspaces outside this set are not loaded — there
/// is no key path, so encrypted content from them must not surface in search.
///
/// Returned sessions are owned by the caller. The caller MUST dispose every value
/// in the dictionary (typically in a <c>finally</c> block) — each session holds
/// <see cref="SecureBytes"/> with live key material.
/// </summary>
public class SearchSessionLoader(
    WorkspaceCache workspaceCache)
{
    public async ValueTask<WorkspaceEncryptionSessions> Load(
        UserContext user,
        SecureBytes? privateKey,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        // No private key in the session cookie ⇒ no path to unseal anything. Return
        // an empty bundle; encrypted-search SQL will then match nothing for this caller.
        if (privateKey is null)
            return WorkspaceEncryptionSessions.Empty;

        var candidateWorkspaceIds = GetCandidateWorkspaceIds(
            user,
            connection);

        if (candidateWorkspaceIds.Count == 0)
            return WorkspaceEncryptionSessions.Empty;

        var sessions = new Dictionary<int, WorkspaceEncryptionSession>();
        try
        {
            foreach (var workspaceId in candidateWorkspaceIds)
            {
                var workspace = await workspaceCache.TryGetWorkspace(
                    workspaceId,
                    cancellationToken);

                if (workspace is null)
                    continue;

                if (workspace.Storage.Encryption is not FullStorageEncryption)
                    continue;

                if (workspace.EncryptionMetadata is null)
                    continue;

                WorkspaceDekEntry[] entries;
                try
                {
                    entries = user.UnsealWorkspaceDeks(
                        workspace: workspace,
                        privateKey: privateKey);
                }
                catch (StorageDekUnsealException e)
                {
                    Log.Error(e,
                        "Search: failed to unseal storage DEK v{Version} for User#{UserId} on Storage#{StorageId}; skipping Workspace#{WorkspaceId}.",
                        e.StorageDekVersion, user.Id, e.StorageId, workspaceId);
                    continue;
                }
                catch (WorkspaceDekUnsealException e)
                {
                    Log.Error(e,
                        "Search: failed to unseal workspace DEK v{Version} for User#{UserId} on Workspace#{WorkspaceId}; skipping.",
                        e.StorageDekVersion, user.Id, e.WorkspaceId);
                    continue;
                }

                if (entries.Length == 0)
                    continue;

                sessions[workspaceId] = new WorkspaceEncryptionSession(
                    workspaceId: workspaceId,
                    entries: entries);
            }
        }
        catch
        {
            foreach (var session in sessions.Values)
                session.Dispose();
            throw;
        }

        return new WorkspaceEncryptionSessions(sessions);
    }

    /// <summary>
    /// Workspaces this caller may have a Workspace DEK for: every workspace they hold a
    /// per-workspace wrap on, plus every workspace whose storage they hold a sek wrap on
    /// (sek-derivation path). This is a pure decryption-capability set; soft-delete and
    /// search-visibility filtering belong to the visibility query, not here.
    /// </summary>
    private static HashSet<int> GetCandidateWorkspaceIds(
        UserContext user,
        SqliteConnection connection)
    {
        var result = new HashSet<int>();

        foreach (var wrap in user.WrappedWorkspaceDeks)
            result.Add(wrap.WorkspaceId);

        var sekStorageIds = user.WrappedStorageDeks
            .Select(s => s.StorageId)
            .Distinct()
            .ToList();

        if (sekStorageIds.Count == 0)
            return result;

        var sekDerivedWorkspaceIds = connection
            .Cmd(
                sql: """
                     SELECT w_id
                     FROM w_workspaces
                     WHERE w_storage_id IN (SELECT value FROM json_each($storageIds))
                     """,
                readRowFunc: r => r.GetInt32(0))
            .WithJsonParameter("$storageIds", sekStorageIds)
            .Execute();

        foreach (var id in sekDerivedWorkspaceIds)
            result.Add(id);

        return result;
    }
}

/// <summary>
/// Owned bundle of <see cref="WorkspaceEncryptionSession"/>s built for a single search
/// request. Disposing the bundle disposes every session inside, wiping the
/// <see cref="SecureBytes"/> DEKs they hold.
/// </summary>
public sealed class WorkspaceEncryptionSessions : IDisposable
{
    public static WorkspaceEncryptionSessions Empty { get; } = new(new Dictionary<int, WorkspaceEncryptionSession>());

    public IReadOnlyDictionary<int, WorkspaceEncryptionSession> ByWorkspaceId { get; }

    internal WorkspaceEncryptionSessions(Dictionary<int, WorkspaceEncryptionSession> sessions)
    {
        ByWorkspaceId = sessions;
    }

    public bool ContainsWorkspace(int workspaceId) => ByWorkspaceId.ContainsKey(workspaceId);

    public void Dispose()
    {
        foreach (var session in ByWorkspaceId.Values)
            session.Dispose();
    }
}
