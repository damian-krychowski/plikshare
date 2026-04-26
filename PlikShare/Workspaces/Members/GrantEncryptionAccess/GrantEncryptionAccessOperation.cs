using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Encryption;
using Serilog;

namespace PlikShare.Workspaces.Members.GrantEncryptionAccess;

/// <summary>
/// Re-wraps every Workspace DEK version the caller holds for the target member, so the target
/// can decrypt files in a full-encryption workspace. Inputs: the owner's already-unsealed DEKs
/// (carried on the <see cref="WorkspaceEncryptionSession"/> populated by the HTTP filter) and
/// the target's public key (read off their <see cref="UserContext.EncryptionMetadata"/>).
///
/// Sealing is pure crypto and is computed up-front (<see cref="BuildWrapped"/>) so the SQLite
/// write loop is not blocked on it. The DB writes — per-version wek upserts plus the optional
/// "access granted" email enqueue — go through <see cref="ExecuteTransaction"/> so a caller
/// with its own transaction (e.g. invite-and-auto-grant) can compose them atomically. The
/// stand-alone <see cref="Execute"/> wrapper is used by the manual grant endpoint.
/// </summary>
public class GrantEncryptionAccessOperation(
    DbWriteQueue dbWriteQueue,
    UpsertWorkspaceEncryptionKeyQuery upsertWorkspaceEncryptionKeyQuery,
    IQueue queue,
    IClock clock)
{
    /// <summary>
    /// Pre-computes the per-version sealed wraps. Runs outside the DB write queue because
    /// sealing is CPU work and we don't want to block the single SQLite writer on it.
    /// </summary>
    public static WrappedVersion[] BuildWrapped(
        WorkspaceEncryptionSession ownerSession,
        TargetUser target)
    {
        if (target.EncryptionMetadata is null)
            throw new InvalidOperationException(
                $"Cannot grant encryption access to User#{target.Id}: encryption metadata is not set. " +
                "The caller must validate this before invoking the operation.");

        return ownerSession
            .Entries
            .Select(entry => new WrappedVersion(
                StorageDekVersion: entry.StorageDekVersion,
                WrappedDek: entry.Dek.Use(
                    state: target.EncryptionMetadata.PublicKey,
                    (dekSpan, pubKey) => UserKeyPair.SealTo(pubKey, dekSpan))))
            .ToArray();
    }

    /// <summary>
    /// Writes the wek upserts (and optionally enqueues the "access granted" email) inside the
    /// caller's transaction. Set <paramref name="notifyTarget"/> to <c>false</c> when the grant
    /// is paired with another notification the target will already receive (e.g. the invitation
    /// email from auto-grant-on-invite) so they don't get a redundant follow-up.
    /// </summary>
    public void ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        WorkspaceContext workspace,
        UserContext owner,
        TargetUser target,
        WrappedVersion[] wrapped,
        Guid correlationId,
        bool notifyTarget)
    {
        foreach (var w in wrapped)
        {
            upsertWorkspaceEncryptionKeyQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                workspaceId: workspace.Id,
                userId: target.Id,
                storageDekVersion: w.StorageDekVersion,
                wrappedWorkspaceDek: w.WrappedDek,
                wrappedByUserId: owner.Id,
                transaction: transaction);
        }

        if (notifyTarget)
        {
            queue.Enqueue(
                correlationId: correlationId,
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<WorkspaceEncryptionKeyGrantApprovedEmailDefinition>
                {
                    Email = target.Email.Value,
                    Template = EmailTemplate.WorkspaceEncryptionKeyGrantApproved,
                    Definition = new WorkspaceEncryptionKeyGrantApprovedEmailDefinition(
                        OwnerEmail: owner.Email.Value,
                        WorkspaceName: workspace.Name)
                },
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
        }

        Log.Information(
            "Owner User#{OwnerId} granted encryption access on Workspace#{WorkspaceId} " +
            "to User#{TargetId} across {VersionCount} Storage DEK version(s).",
            owner.Id, workspace.Id, target.Id, wrapped.Length);
    }

    public Task<int> Execute(
        WorkspaceContext workspace,
        UserContext owner,
        TargetUser target,
        WorkspaceEncryptionSession ownerSession,
        Guid correlationId,
        CancellationToken cancellationToken,
        bool notifyTarget = true)
    {
        var wrapped = BuildWrapped(ownerSession, target);

        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                using var transaction = context.Connection.BeginTransaction();

                try
                {
                    ExecuteTransaction(
                        dbWriteContext: context,
                        transaction: transaction,
                        workspace: workspace,
                        owner: owner,
                        target: target,
                        wrapped: wrapped,
                        correlationId: correlationId,
                        notifyTarget: notifyTarget);

                    transaction.Commit();
                    return wrapped.Length;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            },
            cancellationToken: cancellationToken);
    }

    public readonly record struct WrappedVersion(
        int StorageDekVersion,
        byte[] WrappedDek);
    
    public readonly record struct TargetUser(
        int Id,
        Email Email,
        UserEncryptionMetadata? EncryptionMetadata);
}
