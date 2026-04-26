using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Encryption;
using Serilog;

namespace PlikShare.Workspaces.ChangeOwner;

/// <summary>
/// Atomic ownership transfer for a workspace. For full-encryption workspaces the actor
/// (admin) must hold a key path to the workspace DEK — either a wek wrap on the
/// workspace itself or a sek wrap on its storage — and must arrive with an unlocked
/// encryption session, so we can re-wrap the DEK to the new owner inside the same
/// transaction. The previous owner's wek rows are wiped on transfer; ownership has
/// changed, so retaining their decryption capability would be a leak.
/// </summary>
public class ChangeWorkspaceOwnerQuery(
    DbWriteQueue dbWriteQueue,
    UpsertWorkspaceEncryptionKeyQuery upsertWorkspaceEncryptionKeyQuery)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        UserContext newOwner,
        UserContext actor,
        SecureBytes? actorPrivateKey,
        CancellationToken cancellationToken)
    {
        if (workspace.Storage.Encryption is not FullStorageEncryption)
        {
            return dbWriteQueue.Execute(
                operationToEnqueue: context => ExecuteOperation(
                    dbWriteContext: context,
                    workspace: workspace,
                    newOwner: newOwner,
                    wrapped: []),
                cancellationToken: cancellationToken);
        }

        if (newOwner.EncryptionMetadata is null)
            return Task.FromResult(ResultCode.TargetEncryptionNotSetUp);

        if (actorPrivateKey is null)
            return Task.FromResult(ResultCode.ActorEncryptionSessionRequired);

        if (workspace.EncryptionMetadata is null)
            throw new InvalidOperationException(
                $"Workspace#{workspace.Id} is on a full-encryption storage but has no EncryptionMetadata.");

        WorkspaceDekEntry[] entries;
        try
        {
            entries = actor.UnsealWorkspaceDeks(
                workspace: workspace,
                privateKey: actorPrivateKey);
        }
        catch (WorkspaceDekUnsealException e)
        {
            Log.Error(e,
                "ChangeWorkspaceOwner: failed to unseal workspace DEK v{Version} for actor User#{ActorId} on Workspace#{WorkspaceId}.",
                e.StorageDekVersion, actor.Id, e.WorkspaceId);
            return Task.FromResult(ResultCode.ActorCannotDecryptWorkspace);
        }
        catch (StorageDekUnsealException e)
        {
            Log.Error(e,
                "ChangeWorkspaceOwner: failed to unseal storage DEK v{Version} for actor User#{ActorId} on Storage#{StorageId}.",
                e.StorageDekVersion, actor.Id, e.StorageId);
            return Task.FromResult(ResultCode.ActorCannotDecryptWorkspace);
        }

        if (entries.Length == 0)
            return Task.FromResult(ResultCode.ActorCannotDecryptWorkspace);

        WrappedVersion[] wrapped;
        try
        {
            wrapped = entries
                .Select(entry => new WrappedVersion(
                    StorageDekVersion: entry.StorageDekVersion,
                    WrappedDek: entry.Dek.Use(
                        state: newOwner.EncryptionMetadata.PublicKey,
                        (dekSpan, pubKey) => UserKeyPair.SealTo(pubKey, dekSpan))))
                .ToArray();
        }
        finally
        {
            foreach (var entry in entries)
                entry.Dispose();
        }

        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                newOwner: newOwner,
                wrapped: wrapped),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        UserContext newOwner,
        WrappedVersion[] wrapped)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        try
        {
            var previousOwnerId = workspace.Owner.Id;

            dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE w_workspaces
                         SET w_owner_id = $newOwnerId
                         WHERE w_id = $workspaceId
                         RETURNING w_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$newOwnerId", newOwner.Id)
                .WithParameter("$workspaceId", workspace.Id)
                .ExecuteOrThrow();

            var membershipDeletion = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM wm_workspace_membership
                         WHERE
                             wm_workspace_id = $workspaceId
                             AND wm_member_id = $newOwnerId
                         RETURNING
                             wm_workspace_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$newOwnerId", newOwner.Id)
                .WithParameter("$workspaceId", workspace.Id)
                .Execute();

            foreach (var w in wrapped)
            {
                upsertWorkspaceEncryptionKeyQuery.ExecuteTransaction(
                    dbWriteContext: dbWriteContext,
                    workspaceId: workspace.Id,
                    userId: newOwner.Id,
                    storageDekVersion: w.StorageDekVersion,
                    wrappedWorkspaceDek: w.WrappedDek,
                    wrappedByUserId: previousOwnerId,
                    transaction: transaction);
            }

            if (wrapped.Length > 0)
            {
                dbWriteContext
                    .Connection
                    .NonQueryCmd(
                        sql: """
                             DELETE FROM wek_workspace_encryption_keys
                             WHERE wek_workspace_id = $workspaceId
                               AND wek_user_id = $previousOwnerId
                             """,
                        transaction: transaction)
                    .WithParameter("$workspaceId", workspace.Id)
                    .WithParameter("$previousOwnerId", previousOwnerId)
                    .Execute();
            }

            transaction.Commit();

            Log.Information(
                "Workspace#{WorkspaceId} owner was changed from User#{PreviousOwnerId} to User#{NewOwnerId}. " +
                "wek wraps written for new owner: {NewOwnerVersions}; previous owner wek rows wiped: {WiPedPreviousOwnerWek}; " +
                "membership row removed for new owner: {MembershipRemoved}.",
                workspace.Id, previousOwnerId, newOwner.Id,
                wrapped.Length, wrapped.Length > 0, !membershipDeletion.IsEmpty);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while changing owner of Workspace '{WorkspaceExternalId}' to User '{UserExternalId}'",
                workspace.ExternalId,
                newOwner.ExternalId);

            throw;
        }
    }

    private readonly record struct WrappedVersion(
        int StorageDekVersion,
        byte[] WrappedDek);

    public enum ResultCode
    {
        Ok = 0,
        TargetEncryptionNotSetUp,
        ActorEncryptionSessionRequired,
        ActorCannotDecryptWorkspace
    }
}
